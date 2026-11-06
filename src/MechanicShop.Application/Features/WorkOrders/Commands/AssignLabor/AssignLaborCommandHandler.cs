using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;

public sealed class AssignLaborCommandHandler(
    IAppDbContext context,
    ILogger<AssignLaborCommandHandler> logger,
    HybridCache cache,
    IWorkOrderPolicy workOrderPolicy

) : IRequestHandler<AssignLaborCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<AssignLaborCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = workOrderPolicy;

    public async Task<Result<Updated>> Handle(AssignLaborCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == request.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            _logger.LogError("WorkOrder with Id '{WorkOrderId}' does not exist.", request.WorkOrderId);

            return ApplicationErrors.WorkOrderNotFound;
        }

        var labor = await _context.Employees
            .FindAsync([request.LaborId], cancellationToken);

        if (labor is null)
        {
            _logger.LogError("Invalid LaborId: {LaborId}", request.LaborId);

            return ApplicationErrors.LaborNotFound;
        }

        if (!workOrder.IsEditable)
        {
            _logger.LogError("WorkOrder '{WorkOrderId}' is read-only in state {State}.", workOrder.Id, workOrder.State);

            return WorkOrderErrors.Readonly;
        }

        if (await _workOrderPolicy.IsLaborOccupied(request.LaborId, request.WorkOrderId, workOrder.StartAtUtc, workOrder.EndAtUtc))
        {
            _logger.LogError("Labor with Id '{LaborId}' is already occupied during the requested time.", request.LaborId);

            return ApplicationErrors.LaborOccupied;
        }

        var updateLaborResult = workOrder.UpdateLabor(request.LaborId);

        if (updateLaborResult.IsError)
        {
            foreach (var error in updateLaborResult.Errors)
            {
                _logger.LogError("[LaborUpdate] {ErrorCode}: {ErrorDescription}", error.Code, error.Description);
            }

            return updateLaborResult.Errors;
        }

        workOrder.AddDomainEvent(new WorkOrderCollectionModified());

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync("work-order", cancellationToken);

        return Result.Updated;
    }
}