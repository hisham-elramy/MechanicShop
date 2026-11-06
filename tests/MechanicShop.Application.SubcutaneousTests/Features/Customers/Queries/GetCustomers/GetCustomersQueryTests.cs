using MechanicShop.Application.Features.Customers.Queries.GetCustomers;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomers;

public class GetCustomersQueryTests
{
    [Fact]
    public void CacheKey_ShouldBeEqual_ForEveryInstance()
    {
        // Arrange & Act & Assert
        Assert.Equal(new GetCustomersQuery().CacheKey, new GetCustomersQuery().CacheKey);
    }

    [Fact]
    public void Tags_ShouldContainCustomerTag()
    {
        // Arrange & Act & Assert
        Assert.Contains("customer", new GetCustomersQuery().Tags);
    }
}