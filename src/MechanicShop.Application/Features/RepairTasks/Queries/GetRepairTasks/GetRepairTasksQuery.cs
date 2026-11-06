using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;

public sealed record GetRepairTasksQuery
: ICachedQuery<Result<List<RepairTaskDto>>>
{
    public string CacheKey => "repair-task";

    public string[] Tags => ["repair-task"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}