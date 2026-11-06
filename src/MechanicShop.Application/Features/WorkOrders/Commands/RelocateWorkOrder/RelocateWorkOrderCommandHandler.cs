using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;

public sealed class RelocateWorkOrderCommandHandler(
    IAppDbContext context,
    ILogger<RelocateWorkOrderCommandHandler> logger,
    HybridCache cache,
    IWorkOrderPolicy workOrderPolicy
) : IRequestHandler<RelocateWorkOrderCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RelocateWorkOrderCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = workOrderPolicy;

    public async Task<Result<Updated>> Handle(
        RelocateWorkOrderCommand request,
        CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .Include(wo => wo.Vehicle)
            .Include(wo => wo.RepairTasks)
            .Include(wo => wo.Labor)
            .FirstOrDefaultAsync(
                wo => wo.Id == request.WorkOrderId,
                cancellationToken);

        if (workOrder is null)
        {
            _logger.LogError(
                "WorkOrder with Id '{WorkOrderId}' does not exist.",
                request.WorkOrderId);

            return ApplicationErrors.WorkOrderNotFound;
        }

        // Preserve the original duration.
        var duration = workOrder.EndAtUtc - workOrder.StartAtUtc;

        var endAt = request.NewStartAt.Add(duration);

        // 1. Validate operating hours.
        if (_workOrderPolicy.IsOutsideOperatingHours(
            request.NewStartAt,
            duration))
        {
            _logger.LogError(
                "Relocation failed: requested time ({StartAt} - {EndAt}) is outside operating hours.",
                request.NewStartAt,
                endAt);

            return ApplicationErrors.WorkOrderOutsideOperatingHour(
                request.NewStartAt,
                endAt);
        }

        // 2. Check Spot availability.
        var checkSpotAvailabilityResult =
            await _workOrderPolicy.CheckSpotAvailabilityAsync(
                request.NewSpot,
                request.NewStartAt,
                endAt,
                excludedWorkOrderId: workOrder.Id,
                cancellationToken: cancellationToken);

        if (checkSpotAvailabilityResult.IsError)
        {
            _logger.LogError(
                "Spot '{Spot}' is not available.",
                request.NewSpot);

            return checkSpotAvailabilityResult.Errors;
        }

        // 3. Check Labor availability.
        if (await _workOrderPolicy.IsLaborOccupied(
            workOrder.LaborId,
            workOrder.Id,
            request.NewStartAt,
            endAt))
        {
            _logger.LogError(
                "Labor '{LaborId}' is already occupied during the requested time.",
                workOrder.LaborId);

            return ApplicationErrors.LaborOccupied;
        }

        // 4. Check Vehicle availability.
        if (await _workOrderPolicy.IsVehicleAlreadyScheduled(
            workOrder.VehicleId,
            request.NewStartAt,
            endAt,
            workOrder.Id))
        {
            _logger.LogError(
                "Vehicle '{VehicleId}' already has an overlapping WorkOrder.",
                workOrder.VehicleId);

            return ApplicationErrors.VehicleSchedulingConflict;
        }

        // 5. Update timing.
        var updateTimingResult = workOrder.UpdateTiming(
            request.NewStartAt,
            endAt);

        if (updateTimingResult.IsError)
        {
            _logger.LogError(
                "Failed to update timing: {Error}",
                updateTimingResult.TopError.Description);

            return updateTimingResult.Errors;
        }

        // 6. Update Spot only when it actually changes.
        if (workOrder.Spot != request.NewSpot)
        {
            var updateSpotResult = workOrder.UpdateSpot(request.NewSpot);

            if (updateSpotResult.IsError)
            {
                _logger.LogError(
                    "Failed to update Spot: {Error}",
                    updateSpotResult.TopError.Description);

                return updateSpotResult.Errors;
            }
        }

        // 7. Invalidate cached WorkOrders.
        workOrder.AddDomainEvent(new WorkOrderCollectionModified());

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(
            "work-order",
            cancellationToken);

        return Result.Updated;
    }
}