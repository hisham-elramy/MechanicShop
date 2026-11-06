using MechanicShop.Application.Features.Billing.Queries.GetInvoicePdf;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoicePdf;

public class GetInvoicePdfQueryValidatorTests
{
    private readonly GetInvoicePdfQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenInvoiceIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new GetInvoicePdfQuery(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GetInvoicePdfQuery.InvoiceId) &&
            e.ErrorCode == "InvoiceId_Is_Required");
    }

    [Fact]
    public void Validate_ShouldPass_WhenInvoiceIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new GetInvoicePdfQuery(Guid.NewGuid())).IsValid);
    }

}