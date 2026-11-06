using MechanicShop.Application.Features.Labors.Queries.GetLabors;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Labors.Queries.GetLabors;

public class GetLaborsQueryTests
{
    [Fact]
    public void CacheKey_ShouldBeEqual_ForEveryInstance()
    {
        // Arrange & Act & Assert
        Assert.Equal(new GetLaborsQuery().CacheKey, new GetLaborsQuery().CacheKey);
    }

    [Fact]
    public void Tags_ShouldContainLaborsTag()
    {
        // Arrange & Act & Assert
        Assert.Contains("labors", new GetLaborsQuery().Tags);
    }
}