using MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public class UpdateWorkOrderRepairTasksCommandValidatorTests
{
    private readonly UpdateWorkOrderRepairTasksCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenWorkOrderIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new UpdateWorkOrderRepairTasksCommand(Guid.Empty, [Guid.NewGuid()]));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains
        (
            result.Errors, e =>
            e.PropertyName == nameof(UpdateWorkOrderRepairTasksCommand.WorkOrderId) &&
            e.ErrorCode == "WorkOrderId_Required"
        );
    }

    [Fact]
    public void Validate_ShouldFail_WhenRepairTaskIdsIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), []));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains
        (
            result.Errors, e =>
            e.PropertyName == nameof(UpdateWorkOrderRepairTasksCommand.RepairTaskIds) &&
            e.ErrorCode == "RepairTasks_Required"
        );
    }

    [Fact]
    public void Validate_ShouldFail_WhenRepairTaskIdsContainsEmptyGuid()
    {
        // Arrange & Act
        var result = _validator.Validate(new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), [Guid.Empty]));

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenRepairTaskIdsContainsDuplicates()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var result = _validator.Validate(new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), [id, id]));

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act
        var result = _validator.Validate(new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), [Guid.NewGuid()]));

        // Assert
        Assert.True(result.IsValid);
    }
}