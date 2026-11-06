using MechanicShop.Application.Features.Billing.Queries.GetInvoiceById;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoiceById;

public class GetInvoiceByIdQueryValidatorTests
{
    private readonly GetInvoiceByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenInvoiceIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new GetInvoiceByIdQuery(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GetInvoiceByIdQuery.InvoiceId) &&
            e.ErrorCode == "InvoiceId_Is_Required");
    }

    [Fact]
    public void Validate_ShouldPass_WhenInvoiceIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new GetInvoiceByIdQuery(Guid.NewGuid())).IsValid);
    }

}