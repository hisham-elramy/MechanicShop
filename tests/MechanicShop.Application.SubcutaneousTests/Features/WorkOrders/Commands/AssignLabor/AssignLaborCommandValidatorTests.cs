using MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.AssignLabor;

public class AssignLaborCommandValidatorTests
{
    private readonly AssignLaborCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenWorkOrderIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new AssignLaborCommand(Guid.Empty, Guid.NewGuid()));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(AssignLaborCommand.WorkOrderId) &&
            e.ErrorCode == "WorkOrderId_Required");
    }

    [Fact]
    public void Validate_ShouldFail_WhenLaborIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new AssignLaborCommand(Guid.NewGuid(), Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(AssignLaborCommand.LaborId) &&
            e.ErrorCode == "LaborId_Required");
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act
        var result = _validator.Validate(new AssignLaborCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        Assert.True(result.IsValid);
    }

}