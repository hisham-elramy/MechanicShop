using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTasks;

public class GetRepairTasksQueryTests
{
    [Fact]
    public void CacheKey_ShouldBeEqual_ForEveryInstance()
    {
        Assert.Equal(new GetRepairTasksQuery().CacheKey, new GetRepairTasksQuery().CacheKey);
    }


    [Fact]
    public void Tags_ShouldShareTagWithGetRepairTaskById()
    {
        var single = new GetRepairTaskByIdQuery(Guid.NewGuid());

        Assert.Contains(single.Tags, tag => new GetRepairTasksQuery().Tags.Contains(tag));
    }
}