using MechanicShop.Application.Features.RepairTasks.Commands.RemoveRepairTask;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.RemoveRepairTask;

public class RemoveRepairTaskCommandValidatorTests
{
    private readonly RemoveRepairTaskCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenRepairTaskIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new RemoveRepairTaskCommand(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemoveRepairTaskCommand.RepairTaskId));
    }

    [Fact]
    public void Validate_ShouldPass_WhenRepairTaskIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new RemoveRepairTaskCommand(Guid.NewGuid())).IsValid);
    }
}