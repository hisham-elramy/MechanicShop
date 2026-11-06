using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrders;

public class GetWorkOrdersQueryTests
{
    [Fact]
    public void CacheKey_ShouldDiffer_WhenOnlyTimeOfDayDiffers()
    {
        // Arrange
        var day = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var a = new GetWorkOrdersQuery(1, 10, null, StartDateFrom: day.AddHours(9));
        var b = new GetWorkOrdersQuery(1, 10, null, StartDateFrom: day.AddHours(15));

        // Assert
        Assert.NotEqual(a.CacheKey, b.CacheKey);
    }

    [Fact]
    public void CacheKey_ShouldDiffer_WhenSearchTermContainsDelimiters()
    {
        // Arrange & Act
        var plain = new GetWorkOrdersQuery(1, 10, null, SortColumn: "x");
        var forged = new GetWorkOrdersQuery(1, 10, ":sort=x:desc:state=-", SortColumn: "x");

        // Assert
        Assert.NotEqual(plain.CacheKey, forged.CacheKey);
    }

    [Fact]
    public void CacheKey_ShouldBeEqual_WhenQueriesAreIdentical()
    {
        // Arrange & Act & Assert
        Assert.Equal(
            new GetWorkOrdersQuery(2, 20, "abc").CacheKey,
            new GetWorkOrdersQuery(2, 20, "abc").CacheKey);
    }
}