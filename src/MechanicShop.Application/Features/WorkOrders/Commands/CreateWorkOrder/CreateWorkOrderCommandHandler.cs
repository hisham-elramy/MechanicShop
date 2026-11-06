using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandHandler(
    IAppDbContext context,
    ILogger<CreateWorkOrderCommand> logger,
    HybridCache cache,
    IWorkOrderPolicy workOrderValidator

) : IRequestHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateWorkOrderCommand> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IWorkOrderPolicy _workOrderValidator = workOrderValidator;

    public async Task<Result<WorkOrderDto>> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var repairTasks = await _context.RepairTasks
            .Where(rt => request.RepairTaskIds.Contains(rt.Id))
            .ToListAsync();

        if (repairTasks.Count() != request.RepairTaskIds.Count())
        {
            var missingIds = request.RepairTaskIds.Except(repairTasks.Select(rt => rt.Id)).ToArray();

            _logger.LogError("Some RepairTaskIds not found: {MissingIds}", string.Join(",", missingIds));

            return ApplicationErrors.RepairTaskNotFound;
        }

        var totalEstimatedDuration = TimeSpan.FromMinutes(repairTasks.Sum(rt => (int)rt.EstimatedDurationInMins));
        var endAt = request.StartAt.Add(totalEstimatedDuration);

        if (_workOrderValidator.IsOutsideOperatingHours(request.StartAt, totalEstimatedDuration))
        {
            _logger.LogError("The WorkOrder time ({StartAt} ? {EndAt}) is outside of store operating hours.", request.StartAt, endAt);

            return ApplicationErrors.WorkOrderOutsideOperatingHour(request.StartAt, endAt);
        }

        var checkMinRequirementResult = _workOrderValidator.ValidateMinimumRequirements(request.StartAt, endAt);

        if (checkMinRequirementResult.IsError)
        {
            _logger.LogError("WorkOrder duration is shorter than the configured minimum.");

            return checkMinRequirementResult.Errors;
        }

        var checkSpotAvailabilityResult = await _workOrderValidator
            .CheckSpotAvailabilityAsync
            (
                request.Spot,
                request.StartAt,
                endAt,
                excludedWorkOrderId: null,
                cancellationToken
            );
    
        if (checkSpotAvailabilityResult.IsError)
        {
            _logger.LogError("Spot: {Spot} is not available.", request.Spot.ToString());

            return checkSpotAvailabilityResult.Errors;
        }

        var vehicle = await _context.Vehicles.Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            _logger.LogError("Vehicle with Id '{VehicleId}' does not exist.", request.VehicleId);
        
            return ApplicationErrors.VehicleNotFound;
        }

        var labor = await _context.Employees.FindAsync([request.LaborId], cancellationToken);
    
        if (labor is null)
        {
            _logger.LogError("Invalid LaborId: {LaborId}", request.LaborId.ToString());

            return ApplicationErrors.LaborNotFound;
        }

        var hasVehicleConflict = await _context.WorkOrders
            .AnyAsync
            (
                wo => wo.VehicleId == request.VehicleId &&
                wo.StartAtUtc.Date == request.StartAt.Date &&
                wo.StartAtUtc < endAt &&
                wo.EndAtUtc > request.StartAt,
                cancellationToken
            );

        if (hasVehicleConflict)
        {
            _logger.LogError("Vehicle with Id '{VehicleId}' already has an overlapping WorkOrder.", request.VehicleId);

            return Error.Conflict
            (
                code: "Vehicle_Overlapping_WorkOrder",
                description: "The vehicle already has an overlapping WorkOrder."
            );
        }

        var isLaborOccupied = await _context.WorkOrders
            .AnyAsync
            (
                wo => wo.LaborId == request.LaborId &&
                wo.StartAtUtc < endAt &&
                wo.EndAtUtc > request.StartAt,
                cancellationToken
            );

        if (isLaborOccupied)
        {
            _logger.LogError("Labor with Id '{LaborId}' is already occupied during the requested time.", request.LaborId);

            return Error.Conflict
            (
                code: "Labor_Occupied",
                description: "Labor is already occupied during the requested time."
            );
        }

        var createWorkOrderResult = WorkOrder
            .Create
            (
                Guid.NewGuid(),
                request.VehicleId,
                request.StartAt,
                endAt,
                request.LaborId!.Value,
                request.Spot,
                repairTasks
            );

        if (createWorkOrderResult.IsError)
        {
            _logger.LogError("Failed to create WorkOrder: {Error}", createWorkOrderResult.TopError.Description);

            return createWorkOrderResult.Errors;
        }

        var workOrder = createWorkOrderResult.Value;

        await _context.WorkOrders.AddAsync(workOrder);

        workOrder.AddDomainEvent(new WorkOrderCollectionModified());

        await _context.SaveChangesAsync(cancellationToken);

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        _logger.LogInformation("WorkOrder with Id '{WorkOrderId}' created successfully.", workOrder.Id);

        await _cache.RemoveByTagAsync("work-order", cancellationToken);

        return workOrder.ToDto();
    }
}