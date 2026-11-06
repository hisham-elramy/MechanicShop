using MechanicShop.Application.Features.Billing.Commands.IssueInvoice;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Commands.IssueInvoice;

public class IssueInvoiceCommandValidatorTests
{
    private readonly IssueInvoiceCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenWorkOrderIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new IssueInvoiceCommand(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(IssueInvoiceCommand.WorkOrderId) &&
            e.ErrorCode == "WorkOrderId_Is_Required");
    }

    [Fact]
    public void Validate_ShouldPass_WhenWorkOrderIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new IssueInvoiceCommand(Guid.NewGuid())).IsValid);
    }
}