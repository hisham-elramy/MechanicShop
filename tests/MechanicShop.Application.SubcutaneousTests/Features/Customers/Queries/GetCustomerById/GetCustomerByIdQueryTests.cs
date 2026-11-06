using MechanicShop.Application.Features.Customers.Queries.GetCustomerById;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomerById;

public class GetCustomerByIdQueryTests
{
    [Fact]
    public void CacheKey_ShouldDiffer_WhenIdsDiffer()
    {
        // Arrange & Act & Assert
        Assert.NotEqual(
            new GetCustomerByIdQuery(Guid.NewGuid()).CacheKey,
            new GetCustomerByIdQuery(Guid.NewGuid()).CacheKey);
    }

    [Fact]
    public void CacheKey_ShouldBeEqual_WhenIdsAreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        Assert.Equal(new GetCustomerByIdQuery(id).CacheKey, new GetCustomerByIdQuery(id).CacheKey);
    }

    [Fact]
    public void Tags_ShouldContainCustomerTag()
    {
        // Arrange & Act & Assert
        Assert.Contains("customer", new GetCustomerByIdQuery(Guid.NewGuid()).Tags);
    }
}