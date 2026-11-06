using MechanicShop.Application.Features.RepairTasks.Commands.CreateRepairTask;
using MechanicShop.Domain.RepairTasks.Enums;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.CreateRepairTask;

public class CreateRepairTaskCommandValidatorTests
{
    private readonly CreateRepairTaskCommandValidator _validator = new();

    private static CreateRepairTaskCommand ValidCommand()
    {
        return new
        (
            Name: "Brake Inspection",
            LaborCost: 100m,
            EstimatedDurationInMins: RepairDurationInMinutes.Min30,
            Parts: [new CreateRepairTaskPartCommand("Brake Pad", 50m, 2)]
        );
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenNameIsEmpty(string? name)
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { Name = name });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRepairTaskCommand.Name));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceeds100Characters()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { Name = new string('a', 101) });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRepairTaskCommand.Name));
    }

    [Fact]
    public void Validate_ShouldPass_WhenNameIsExactly100Characters()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(ValidCommand() with { Name = new string('a', 100) }).IsValid);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenLaborCostIsNotPositive(int cost)
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { LaborCost = cost });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRepairTaskCommand.LaborCost));
    }


    [Fact]
    public void Validate_ShouldFail_WhenDurationIsNull()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { EstimatedDurationInMins = null });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRepairTaskCommand.EstimatedDurationInMins));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDurationIsInvalid()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { EstimatedDurationInMins = (RepairDurationInMinutes)999 });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRepairTaskCommand.EstimatedDurationInMins));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPartsIsNull()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { Parts = null! });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRepairTaskCommand.Parts));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPartsIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(ValidCommand() with { Parts = [] });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRepairTaskCommand.Parts));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenPartNameIsEmpty(string name)
    {
        // Arrange
        var command = ValidCommand() with { Parts = [new CreateRepairTaskPartCommand(name, 50m, 2)] };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == "Parts[0].Name");
    }

    [Fact]
    public void Validate_ShouldFail_WhenPartNameExceeds100Characters()
    {
        // Arrange
        var command = ValidCommand() with { Parts = [new CreateRepairTaskPartCommand(new string('a', 101), 50m, 2)] };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == "Parts[0].Name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenPartCostIsNotPositive(int cost)
    {
        // Arrange
        var command = ValidCommand() with { Parts = [new CreateRepairTaskPartCommand("Pad", cost, 2)] };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == "Parts[0].Cost");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenPartQuantityIsNotPositive(int quantity)
    {
        // Arrange
        var command = ValidCommand() with { Parts = [new CreateRepairTaskPartCommand("Pad", 50m, quantity)] };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == "Parts[0].Quantity");
    }

    [Fact]
    public void Validate_ShouldReportTheInvalidPartIndex_WhenOnlySecondPartIsInvalid()
    {
        // Arrange
        var command = ValidCommand() with
        {
            Parts =
            [
                new CreateRepairTaskPartCommand("Pad", 50m, 2),
                new CreateRepairTaskPartCommand("Rotor", 0m, 1),
            ]
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == "Parts[1].Cost");
        Assert.DoesNotContain(result.Errors, e => e.PropertyName.StartsWith("Parts[0]"));
    }


    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(ValidCommand()).IsValid);
    }
}