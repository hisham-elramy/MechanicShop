using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Commands.CreateRepairTask;

public sealed class CreateRepairTaskCommandHandler (
    IAppDbContext context,
    ILogger<CreateRepairTaskCommandHandler> logger,
    HybridCache cache

) : IRequestHandler<CreateRepairTaskCommand, Result<RepairTaskDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateRepairTaskCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;

    public async Task<Result<RepairTaskDto>> Handle(CreateRepairTaskCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (request.EstimatedDurationInMins is null)
        {
            return Error.Validation("RepairTask.DurationRequired", "Estimated duration is required.");
        }

        var nameExists = await _context.RepairTasks.AnyAsync(rt => rt.Name == name, cancellationToken);

        if (nameExists)
        {
            _logger.LogWarning("Duplicate part name '{PartName}'.", request.Name);

            return RepairTaskErrors.DuplicateName;
        }

        List<Part> parts = [];

        foreach(var p in request.Parts)
        {
            var partResult = Part.Create
            (
                id: Guid.NewGuid(),
                name: p.Name,
                cost: p.Cost,
                quantity: p.Quantity
            );

            if (partResult.IsError)
            {
                return partResult.Errors;
            }

            parts.Add(partResult.Value);
        }

        var createdRepairTaskResult = RepairTask.Create
        (
            id: Guid.NewGuid(),
            name: name,
            laborCost: request.LaborCost,
            estimatedDurationInMins: request.EstimatedDurationInMins!.Value,
            parts: parts
        );

        if (createdRepairTaskResult.IsError)
        {
            return createdRepairTaskResult.Errors;
        }

        var repairTask = createdRepairTaskResult.Value;

        _context.RepairTasks.Add(repairTask);
    
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(["repair-task", "repair-tasks"], cancellationToken);

        return repairTask.ToDto();
    }
}