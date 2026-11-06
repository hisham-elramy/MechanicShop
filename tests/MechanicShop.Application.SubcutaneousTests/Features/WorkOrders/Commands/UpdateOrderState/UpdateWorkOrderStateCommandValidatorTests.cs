using MechanicShop.Application.Features.WorkOrders.Commands.UpdateOrderState;
using MechanicShop.Domain.WorkOrders.Enums;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateOrderState;

public class UpdateOrderStateCommandValidatorTests
{
    private readonly UpdateWorkOrderStateCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenStateIsInvalid()
    {
        // Arrange & Act
        var result = _validator.Validate(new UpdateWorkOrderStateCommand(Guid.NewGuid(), (WorkOrderState)999));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains
        (
            result.Errors, e =>
            e.PropertyName == nameof(UpdateWorkOrderStateCommand.State) &&
            e.ErrorCode == "WorkOrderStatus_Invalid"
        );
    }

    [Fact]
    public void Validate_ShouldFail_WhenWorkOrderIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new UpdateWorkOrderStateCommand(Guid.Empty, WorkOrderState.InProgress));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateWorkOrderStateCommand.WorkOrderId));
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act
        var result = _validator.Validate(new UpdateWorkOrderStateCommand(Guid.NewGuid(), WorkOrderState.InProgress));

        // Assert
        Assert.True(result.IsValid);
    }
}