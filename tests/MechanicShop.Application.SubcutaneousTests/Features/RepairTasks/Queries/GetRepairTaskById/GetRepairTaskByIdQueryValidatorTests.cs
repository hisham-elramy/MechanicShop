using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTaskById;

public class GetRepairTaskByIdQueryValidatorTests
{
    private readonly GetRepairTaskByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenRepairTaskIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new GetRepairTaskByIdQuery(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains
        (
            result.Errors, e =>
            e.PropertyName == nameof(GetRepairTaskByIdQuery.RepairTaskId) &&
            e.ErrorCode == "RepairTaskId_Is_Required"
        );
    }

    [Fact]
    public void Validate_ShouldPass_WhenRepairTaskIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new GetRepairTaskByIdQuery(Guid.NewGuid())).IsValid);
    }
}