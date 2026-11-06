using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrderById;

public class GetWorkOrderByIdQueryTests
{
    [Fact]
    public void CacheKey_ShouldDiffer_WhenIdsDiffer()
    {
        Assert.NotEqual(
            new GetWorkOrderByIdQuery(Guid.NewGuid()).CacheKey,
            new GetWorkOrderByIdQuery(Guid.NewGuid()).CacheKey);
    }

    [Fact]
    public void CacheKey_ShouldBeEqual_WhenIdsAreEqual()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            new GetWorkOrderByIdQuery(id).CacheKey,
            new GetWorkOrderByIdQuery(id).CacheKey);
    }

    [Fact]
    public void Tags_ShouldContainWorkOrderTag()
    {
        Assert.Contains("work-order", new GetWorkOrderByIdQuery(Guid.NewGuid()).Tags);
    }
}