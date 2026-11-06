using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Commands.UpdateRepairTask;

public sealed class UpdateRepairTaskCommandHandler(
    IAppDbContext context,
    ILogger<UpdateRepairTaskCommandHandler> logger,
    HybridCache cache
)
: IRequestHandler<UpdateRepairTaskCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateRepairTaskCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;

    public async Task<Result<Updated>> Handle(UpdateRepairTaskCommand request, CancellationToken cancellationToken)
    {
        var repairTask = await _context.RepairTasks
            .Include(rt => rt.Parts)
            .FirstOrDefaultAsync(rt => rt.Id == request.RepairTaskId, cancellationToken);

        if (repairTask is null)
        {
            _logger.LogWarning("RepairTask {RepairTaskId} not found for update", request.RepairTaskId);

            return ApplicationErrors.RepairTaskNotFound;
        }

        var name = request.Name.Trim();

        var nameTaken = await _context.RepairTasks
            .AnyAsync(rt => rt.Name == name && rt.Id != request.RepairTaskId, cancellationToken);

        if (nameTaken)
        {
            _logger.LogWarning("Duplicate repair task name '{Name}'.", name);

            return RepairTaskErrors.DuplicateName;
        }

        var existingIds = repairTask.Parts.Select(p => p.Id).ToHashSet();
        var givenIds = request.Parts.Where(p => p.PartId.HasValue).Select(p => p.PartId!.Value).ToList();

        if (givenIds.Any(id => !existingIds.Contains(id)) || givenIds.Distinct().Count() != givenIds.Count)
        {
            return Error.Validation("RepairTask.PartInvalid", "A part id is unknown or duplicated.");
        }

        var validatedParts = new List<Part>();

        foreach (var p in request.Parts)
        {
            var partId = p.PartId ?? Guid.NewGuid();

            var partResult = Part.Create
            (
                id: partId,
                name: p.Name,
                cost: p.Cost,
                quantity: p.Quantity
            );

            if (partResult.IsError)
            {
                return partResult.Errors;
            }

            validatedParts.Add(partResult.Value);
        }

        var updateRepairTaskResult = repairTask.Update(name, request.LaborCost, request.EstimatedDurationInMins);

        if (updateRepairTaskResult.IsError)
        {
            return updateRepairTaskResult.Errors;
        }

        var updatePartsResult = repairTask.UpsertParts(validatedParts);

        if (updatePartsResult.IsError)
        {
            return updatePartsResult.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);
        
        await _cache.RemoveByTagAsync(["repair-task", "repair-tasks"], cancellationToken);

        return Result.Updated;
    }
}