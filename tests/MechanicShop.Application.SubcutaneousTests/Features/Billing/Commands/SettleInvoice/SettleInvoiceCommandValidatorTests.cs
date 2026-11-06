using MechanicShop.Application.Features.Billing.Commands.SettleInvoice;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Commands.SettleInvoice;

public class SettleInvoiceCommandValidatorTests
{
    private readonly SettleInvoiceCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenInvoiceIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new SettleInvoiceCommand(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(SettleInvoiceCommand.InvoiceId) &&
            e.ErrorCode == "InvoiceId_Is_Required");
    }

    [Fact]
    public void Validate_ShouldPass_WhenInvoiceIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new SettleInvoiceCommand(Guid.NewGuid())).IsValid);
    }

}