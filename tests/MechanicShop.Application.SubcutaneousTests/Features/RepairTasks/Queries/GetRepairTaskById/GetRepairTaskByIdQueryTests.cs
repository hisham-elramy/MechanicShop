using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTaskById;

public class GetRepairTaskByIdQueryTests
{
    [Fact]
    public void CacheKey_ShouldDiffer_WhenIdsDiffer()
    {
        // Arrange & Act & Assert
        Assert.NotEqual
        (
            new GetRepairTaskByIdQuery(Guid.NewGuid()).CacheKey,
            new GetRepairTaskByIdQuery(Guid.NewGuid()).CacheKey
        );
    }

    [Fact]
    public void CacheKey_ShouldBeEqual_WhenIdsAreEqual()
    {
        // Arrange & Act
        var id = Guid.NewGuid();

        // Assert
        Assert.Equal
        (
            new GetRepairTaskByIdQuery(id).CacheKey,
            new GetRepairTaskByIdQuery(id).CacheKey
        );
    }

    [Fact]
    public void Tags_ShouldContainRepairTaskTag()
    {
        // Arrange & Act & Assert
        Assert.Contains("repair-task", new GetRepairTaskByIdQuery(Guid.NewGuid()).Tags);
    }
}