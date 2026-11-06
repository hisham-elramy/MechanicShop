using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Tests.Common.RepairTasks; // your factories were shown in ...RepaireTasks; match whichever namespace you use

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class RepairTaskMapperTests
{
    [Fact]
    public void ToDto_RepairTask_ShouldMapCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var part = PartFactory.CreatePart(name: "Oil Filter", cost: 25m, quantity: 2).Value;

        var repairTask = RepairTaskFactory.CreateRepairTask(
            id: id,
            name: "Oil Change",
            laborCost: 150m,
            parts: [part]).Value;

        // Act
        var dto = repairTask.ToDto();

        // Assert
        Assert.Equal(id, dto.RepairTaskId);
        Assert.Equal("Oil Change", dto.Name);
        Assert.Equal(150m, dto.LaborCost);
        Assert.Equal(repairTask.TotalCost, dto.TotalCost);
        Assert.Equal(repairTask.EstimatedDurationInMins, dto.EstimatedDurationInMins);
    }

    [Fact]
    public void ToDto_RepairTask_ShouldMapParts()
    {
        // Arrange
        var part = PartFactory.CreatePart(name: "Oil Filter", cost: 25m, quantity: 2).Value;
        var repairTask = RepairTaskFactory.CreateRepairTask(parts: [part]).Value;

        // Act
        var dto = repairTask.ToDto();

        // Assert
        var partDto = Assert.Single(dto.Parts);
        Assert.Equal(part.Id, partDto.PartId);
        Assert.Equal("Oil Filter", partDto.Name);
        Assert.Equal(25m, partDto.Cost);
        Assert.Equal(2, partDto.Quantity);
    }

    [Fact]
    public void ToDto_RepairTask_WithMultipleParts_ShouldMapAllPartsInOrder()
    {
        // Arrange
        var part1 = PartFactory.CreatePart(name: "Brake Pad", cost: 50m, quantity: 4).Value;
        var part2 = PartFactory.CreatePart(name: "Brake Fluid", cost: 15m, quantity: 1).Value;
        var repairTask = RepairTaskFactory.CreateRepairTask(parts: [part1, part2]).Value;

        // Act
        var dto = repairTask.ToDto();

        // Assert
        Assert.Equal(2, dto.Parts.Count);
        Assert.Equal(part1.Id, dto.Parts[0].PartId);
        Assert.Equal("Brake Pad", dto.Parts[0].Name);
        Assert.Equal(part2.Id, dto.Parts[1].PartId);
        Assert.Equal("Brake Fluid", dto.Parts[1].Name);
    }

    [Fact]
    public void ToDto_RepairTask_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        RepairTask repairTask = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => repairTask.ToDto());
    }


    [Fact]
    public void ToDto_Part_ShouldMapCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var part = PartFactory.CreatePart(id: id, name: "Air Filter", cost: 35m, quantity: 3).Value;

        // Act
        var dto = part.ToDto();

        // Assert
        Assert.Equal(id, dto.PartId);
        Assert.Equal("Air Filter", dto.Name);
        Assert.Equal(35m, dto.Cost);
        Assert.Equal(3, dto.Quantity);
    }

    [Fact]
    public void ToDto_Part_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        Part part = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => part.ToDto());
    }


    [Fact]
    public void ToDtos_RepairTasks_ShouldMapListCorrectly()
    {
        // Arrange
        var task1 = RepairTaskFactory.CreateRepairTask(name: "Oil Change").Value;
        var task2 = RepairTaskFactory.CreateRepairTask(name: "Brake Service").Value;
        var tasks = new List<RepairTask> { task1, task2 };

        // Act
        var dtos = tasks.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(task1.Id, dtos[0].RepairTaskId);
        Assert.Equal("Oil Change", dtos[0].Name);
        Assert.Equal(task2.Id, dtos[1].RepairTaskId);
        Assert.Equal("Brake Service", dtos[1].Name);
    }

    [Fact]
    public void ToDtos_RepairTasks_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var tasks = new List<RepairTask>();

        // Act
        var dtos = tasks.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public void ToDtos_RepairTasks_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<RepairTask> tasks = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tasks.ToDtos());
    }


    [Fact]
    public void ToDtos_Parts_ShouldMapListCorrectly()
    {
        // Arrange
        var part1 = PartFactory.CreatePart(name: "Brake Pad", cost: 50m, quantity: 4).Value;
        var part2 = PartFactory.CreatePart(name: "Brake Fluid", cost: 15m, quantity: 1).Value;
        var parts = new List<Part> { part1, part2 };

        // Act
        var dtos = parts.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(part1.Id, dtos[0].PartId);
        Assert.Equal(50m, dtos[0].Cost);
        Assert.Equal(4, dtos[0].Quantity);
        Assert.Equal(part2.Id, dtos[1].PartId);
        Assert.Equal(15m, dtos[1].Cost);
        Assert.Equal(1, dtos[1].Quantity);
    }

    [Fact]
    public void ToDtos_Parts_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var parts = new List<Part>();

        // Act
        var dtos = parts.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public void ToDtos_Parts_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<Part> parts = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => parts.ToDtos());
    }
}