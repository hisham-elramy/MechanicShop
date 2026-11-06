using MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;
using MechanicShop.Domain.WorkOrders.Enums;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.RelocateWorkOrder;

public class RelocateWorkOrderCommandValidatorTests
{
    private static RelocateWorkOrderCommandValidator _validator => new();

    private static RelocateWorkOrderCommand ValidCommand()
    {
        return new
        (
            WorkOrderId: Guid.NewGuid(),
            NewStartAt: DateTimeOffset.UtcNow.AddHours(1),
            NewSpot: Spot.A
        );
    }

    [Fact]
    public void Validate_ShouldFail_WhenWorkOrderIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { WorkOrderId = Guid.Empty });
    
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RelocateWorkOrderCommand.WorkOrderId));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNewStartAtIsNotInFuture()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { NewStartAt = DateTimeOffset.UtcNow.AddSeconds(-1) });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RelocateWorkOrderCommand.NewStartAt));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSpotIsInvalid()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { NewSpot = (Spot)999 });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RelocateWorkOrderCommand.NewSpot));
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand());

        // Assert
        Assert.True(result.IsValid);
    }

}