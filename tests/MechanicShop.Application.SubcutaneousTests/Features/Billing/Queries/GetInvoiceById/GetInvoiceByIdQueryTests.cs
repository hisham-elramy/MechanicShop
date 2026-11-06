using MechanicShop.Application.Features.Billing.Queries.GetInvoiceById;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoiceById;

public class GetInvoiceByIdQueryTests
{
    [Fact]
    public void CacheKey_ShouldDiffer_WhenIdsDiffer()
    {
        // Arrange & Act & Assert
        Assert.NotEqual
        (
            new GetInvoiceByIdQuery(Guid.NewGuid()).CacheKey,
            new GetInvoiceByIdQuery(Guid.NewGuid()).CacheKey
        );
    }

    [Fact]
    public void CacheKey_ShouldBeEqual_WhenIdsAreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        Assert.Equal(new GetInvoiceByIdQuery(id).CacheKey, new GetInvoiceByIdQuery(id).CacheKey);
    }

    [Fact]
    public void Tags_ShouldContainInvoiceTag()
    {
        // Arrange & Act & Assert
        Assert.Contains("invoice", new GetInvoiceByIdQuery(Guid.NewGuid()).Tags);
    }

}