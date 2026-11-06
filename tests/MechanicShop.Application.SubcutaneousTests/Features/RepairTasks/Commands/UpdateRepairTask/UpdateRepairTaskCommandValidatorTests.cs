using MechanicShop.Application.Features.RepairTasks.Commands.UpdateRepairTask;
using MechanicShop.Domain.RepairTasks.Enums;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.UpdateRepairTask;

public class UpdateRepairTaskCommandValidatorTests
{
    private readonly UpdateRepairTaskCommandValidator _validator = new();

    private static UpdateRepairTaskCommand Valid()
    {
        return new
        (
            Guid.NewGuid(),
            "Brake Inspection",
            100m,
            RepairDurationInMinutes.Min30,
            [new UpdateRepairTaskPartCommand(null, "Pad", 50m, 2)]
        );
    }

    [Fact]
    public void Validate_ShouldFail_WhenRepairTaskIdIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { RepairTaskId = Guid.Empty }).Errors,
            e => e.PropertyName == nameof(UpdateRepairTaskCommand.RepairTaskId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenNameIsEmpty(string name)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Name = name }).Errors,
            e => e.PropertyName == nameof(UpdateRepairTaskCommand.Name));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceeds100Characters()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Name = new string('a', 101) }).Errors,
            e => e.PropertyName == nameof(UpdateRepairTaskCommand.Name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Validate_ShouldFail_WhenLaborCostIsOutOfRange(int cost)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { LaborCost = cost }).Errors,
            e => e.PropertyName == nameof(UpdateRepairTaskCommand.LaborCost));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    public void Validate_ShouldPass_WhenLaborCostIsOnTheBoundary(int cost)
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(Valid() with { LaborCost = cost }).IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDurationIsInvalid()
    {
        // Arrange  & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { EstimatedDurationInMins = (RepairDurationInMinutes)999 }).Errors,
            e => e.PropertyName == nameof(UpdateRepairTaskCommand.EstimatedDurationInMins));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPartsIsNull()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Parts = null! }).Errors,
            e => e.PropertyName == nameof(UpdateRepairTaskCommand.Parts));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPartsIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Parts = [] }).Errors,
            e => e.PropertyName == nameof(UpdateRepairTaskCommand.Parts));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPartNameIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Parts = [new(null, "", 50m, 2)] }).Errors,
            e => e.PropertyName == "Parts[0].Name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Validate_ShouldFail_WhenPartCostIsOutOfRange(int cost)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Parts = [new(null, "Pad", cost, 2)] }).Errors,
            e => e.PropertyName == "Parts[0].Cost");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_ShouldFail_WhenPartQuantityIsOutOfRange(int quantity)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Parts = [new(null, "Pad", 50m, quantity)] }).Errors,
            e => e.PropertyName == "Parts[0].Quantity");
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(Valid()).IsValid);
    }
}