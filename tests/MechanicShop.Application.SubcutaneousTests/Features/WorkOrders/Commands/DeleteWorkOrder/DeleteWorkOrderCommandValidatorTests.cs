using MechanicShop.Application.Features.WorkOrders.Commands.DeleteWorkOrder;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.DeleteWorkOrder;

public class DeleteWorkOrderCommandValidatorTests
{
    private readonly DeleteWorkOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenWorkOrderIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new DeleteWorkOrderCommand(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert
        .Contains
        (
            result.Errors, e =>
            e.PropertyName == nameof(DeleteWorkOrderCommand.WorkOrderId) &&
            e.ErrorCode == "WorkOrderId_Required"
        );
    }

    [Fact]
    public void Validate_ShouldPass_WhenWorkOrderIdIsProvided()
    {
        var result = _validator.Validate(new DeleteWorkOrderCommand(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }


}