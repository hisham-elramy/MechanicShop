using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public sealed class UpdateWorkOrderRepairTasksCommandHandler(
    IAppDbContext context,
    ILogger<UpdateWorkOrderRepairTasksCommandHandler> logger,
    HybridCache cache,
    IWorkOrderPolicy workOrderPolicy

) : IRequestHandler<UpdateWorkOrderRepairTasksCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateWorkOrderRepairTasksCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = workOrderPolicy;

    public async Task<Result<Updated>> Handle(UpdateWorkOrderRepairTasksCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .Include(wo => wo.RepairTasks)
            .FirstOrDefaultAsync(wo => wo.Id == request.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            _logger.LogError("WorkOrder with Id '{WorkOrderId}' does not exist.", request.WorkOrderId);

            return ApplicationErrors.WorkOrderNotFound;
        }

        if (request.RepairTaskIds.Length == 0)
        {
            _logger.LogError("Empty RepairTaskIds list submitted.");

            return RepairTaskErrors.AtLeastOneRepairTaskIsRequired;
        }

        if (!workOrder.IsEditable)
        {
            _logger.LogError("WorkOrder '{WorkOrderId}' is read-only in state {State}.", workOrder.Id, workOrder.State);

            return WorkOrderErrors.Readonly;
        }

        var requestedIds = request.RepairTaskIds.Distinct().ToArray();

        var requestedTasks = await _context.RepairTasks
            .Where(rt => requestedIds.Contains(rt.Id))
            .ToListAsync(cancellationToken);

        if (requestedTasks.Count != requestedIds.Length)
        {
            var missingIds = requestedIds.Except(requestedTasks.Select(rt => rt.Id)).ToArray();

            _logger.LogError("One or more RepairTasks not found. {Ids}", string.Join(", ", missingIds));

            return ApplicationErrors.RepairTaskNotFound;
        }

        var totalDuration = TimeSpan.FromMinutes(requestedTasks.Sum(rt => (int)rt.EstimatedDurationInMins));
        var newEndAt = workOrder.StartAtUtc + totalDuration;

        if (_workOrderPolicy.IsOutsideOperatingHours(workOrder.StartAtUtc, totalDuration))
        {
            _logger.LogError("New duration puts WorkOrder '{WorkOrderId}' outside operating hours.", workOrder.Id);

            return ApplicationErrors.WorkOrderOutsideOperatingHour(workOrder.StartAtUtc, newEndAt);
        }

        var spotCheckResult = await _workOrderPolicy
        .CheckSpotAvailabilityAsync
        (
            workOrder.Spot,
            workOrder.StartAtUtc,
            newEndAt,
            excludedWorkOrderId: workOrder.Id,
            cancellationToken: cancellationToken
        );

        if (spotCheckResult.IsError)
        {
            _logger.LogError("Spot {Spot} is not available for the new duration.", workOrder.Spot);

            return spotCheckResult.Errors;
        }

        if (await _workOrderPolicy.IsLaborOccupied(workOrder.LaborId, workOrder.Id, workOrder.StartAtUtc, newEndAt))
        {
            _logger.LogError("Labor '{LaborId}' is occupied during the new duration.", workOrder.LaborId);

            return ApplicationErrors.LaborOccupied;
        }

        if (await _workOrderPolicy.IsVehicleAlreadyScheduled(workOrder.VehicleId, workOrder.StartAtUtc, newEndAt, workOrder.Id))
        {
            _logger.LogError("Vehicle '{VehicleId}' already has an overlapping WorkOrder.", workOrder.VehicleId);

            return ApplicationErrors.VehicleSchedulingConflict;
        }

        var clearResult = workOrder.ClearRepairTasks();

        if (clearResult.IsError)
        {
            return clearResult.Errors;
        }

        foreach (var task in requestedTasks)
        {
            var addResult = workOrder.AddRepairTask(task);

            if (addResult.IsError)
            {
                return addResult.Errors;
            }
        }

        var updateTimingResult = workOrder.UpdateTiming(workOrder.StartAtUtc, newEndAt);

        if (updateTimingResult.IsError)
        {
            _logger.LogError("Failed to update timing: {Error}", updateTimingResult.TopError.Description);

            return updateTimingResult.Errors;
        }

        workOrder.AddDomainEvent(new WorkOrderCollectionModified());

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync("work-order", cancellationToken);

        return Result.Updated;
    }
}