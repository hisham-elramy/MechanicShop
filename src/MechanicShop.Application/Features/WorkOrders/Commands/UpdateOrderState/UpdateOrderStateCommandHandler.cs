using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateOrderState;

public sealed class UpdateWorkOrderStateCommandHandler(
    IAppDbContext context,
    ILogger<UpdateWorkOrderStateCommandHandler> logger,
    HybridCache cache,
    IAppClock clock

) : IRequestHandler<UpdateWorkOrderStateCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateWorkOrderStateCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IAppClock _clock = clock;

    public async Task<Result<Updated>> Handle(UpdateWorkOrderStateCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(wo => wo.Id == request.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            _logger.LogError("WorkOrder with Id '{WorkOrderId}' does not exist.", request.WorkOrderId);
        
            return ApplicationErrors.WorkOrderNotFound;
        }

        if (workOrder.StartAtUtc > _clock.UtcNow)
        {
            _logger.LogError("State transition for WorkOrder Id '{WorkOrderId}' is not allowed before the work orders scheduled start time.", request.WorkOrderId);

            return WorkOrderErrors.StateTransitionNotAllowed(workOrder.StartAtUtc);
        }

        var updateStatusResult = workOrder.UpdateState(request.State);

        if (updateStatusResult.IsError)
        {
            _logger.LogError("Failed to update status: {Error}", updateStatusResult.TopError.Description);

            return updateStatusResult.Errors;
        }

        if (request.State == WorkOrderState.Completed)
        {
            workOrder.AddDomainEvent(new WorkOrderCompleted { WorkOrderId = request.WorkOrderId });
        }

        workOrder.AddDomainEvent(new WorkOrderCollectionModified());

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync("work-order", cancellationToken);

        return Result.Updated;
    }
}