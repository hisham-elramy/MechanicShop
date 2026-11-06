using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Commands.RemoveRepairTask;

public sealed class RemoveRepairTaskCommandHandler(
    IAppDbContext context,
    ILogger<RemoveRepairTaskCommandHandler> logger,
    HybridCache cache
) :
IRequestHandler<RemoveRepairTaskCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveRepairTaskCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;

    public async Task<Result<Deleted>> Handle(RemoveRepairTaskCommand request, CancellationToken cancellationToken)
    {
        var repairTask = await _context.RepairTasks
            .FirstOrDefaultAsync(rt => rt.Id == request.RepairTaskId, cancellationToken);

        if (repairTask is null)
        {
            _logger.LogWarning("RepairTask {RepairTask} not found for deletion.", request.RepairTaskId);

            return ApplicationErrors.RepairTaskNotFound;
        }

        var isInUse = await _context.WorkOrders
            .AsNoTracking()
            .AnyAsync(wo => wo.RepairTasks.Any(rt => rt.Id == request.RepairTaskId), cancellationToken);

        if (isInUse)
        {
            _logger.LogWarning("RepairTask {RepairTaskId} cannot be deleted - in use by work orders.", request.RepairTaskId);

            return RepairTaskErrors.InUse;
        }

        _context.RepairTasks.Remove(repairTask);

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(["repair-task", "repair-tasks"], cancellationToken);

        _logger.LogInformation("RepairTask {RepairTaskId} deleted successfully.", request.RepairTaskId);

        return Result.Deleted;
    }
}