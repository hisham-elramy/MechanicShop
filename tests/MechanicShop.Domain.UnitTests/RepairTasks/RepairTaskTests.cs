using System.Diagnostics.Tracing;
using System.Reflection;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Infrastructure.Data.Configurations;
using MechanicShop.Tests.Common.RepairTasks;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Xunit;

namespace MechanicShop.Domain.UnitTests.RepairTasks;

public class RepairTaskTests
{
    [Fact]
    public void CreateRepairTask_ShouldSucceed_WithValidData()
    {
        var id = Guid.NewGuid();
        const string name = "SomeTask";
        const decimal laborCost = 100m;
        const decimal partCost = 50m;
        const int partQuantity = 1;
        const RepairDurationInMinutes estimatedDurationInMin = RepairDurationInMinutes.Min30;
        List<Part> parts = [PartFactory.CreatePart(cost: partCost, quantity: partQuantity).Value];

        const decimal totalCost = (partCost * partQuantity) + laborCost;

        var result = RepairTaskFactory.CreateRepairTask
        (
            id: id,
            name: name,
            laborCost: laborCost,
            repairDurationInMinutes: estimatedDurationInMin,
            parts: parts
        );

        Assert.True(result.IsSuccess);

        var task = result.Value;
        Assert.Equal(id, task.Id);
        Assert.Equal(name, task.Name);
        Assert.Equal(laborCost, task.LaborCost);
        Assert.Equal(estimatedDurationInMin, task.EstimatedDurationInMins);
        Assert.Single(task.Parts);
        Assert.Equal(totalCost, task.TotalCost);
    }

    [Fact]
    public void CreateRepairTask_ShouldFail_WithEmptyName()
    {
        const string name = " ";

        var result = RepairTaskFactory.CreateRepairTask
        (
            id: Guid.NewGuid(),
            name: name,
            laborCost: 100,
            repairDurationInMinutes: RepairDurationInMinutes.Min30,
            parts: [PartFactory.CreatePart().Value]
        );

        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.NameRequired.Code, result.TopError.Code);
    }

    [Fact]
    public void CreateRepairTask_ShouldFail_WithInvalidLaborCost()
    {
        const int laborCost = 0;

        var result = RepairTaskFactory.CreateRepairTask
        (
            id: Guid.NewGuid(),
            name: "Brake Inspection",
            laborCost: laborCost,
            repairDurationInMinutes: RepairDurationInMinutes.Min30,
            parts: [PartFactory.CreatePart().Value]
        );

        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.LaborCostInvalid.Code, result.TopError.Code);
    }

    [Fact]
    public void CreateRepairTask_ShouldFail_WithInvalidDuration()
    {
        const RepairDurationInMinutes invalidDurationValue = (RepairDurationInMinutes)999;

        var result = RepairTaskFactory.CreateRepairTask
        (
            id: Guid.NewGuid(),
            name: "Brake Inspection",
            laborCost: 100,
            repairDurationInMinutes: invalidDurationValue,
            parts: [PartFactory.CreatePart().Value]
        );

        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.DurationInvalid.Code, result.TopError.Code);
    }

    [Fact]
    public void UpsertParts_AddNewPart_WhenNotExisting()
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;
        var incoming = PartFactory.CreatePart().Value;

        var result = task.UpsertParts([incoming]);

        Assert.True(result.IsSuccess);
        Assert.Contains(incoming, task.Parts);
    }

    [Fact]
    public void UpsertParts_UpdatesExistingPart_WhenExisting()
    {
        var id = Guid.NewGuid();
        var original = PartFactory.CreatePart(id: id, name: "Old", cost: 10, quantity: 2).Value;
        var task = RepairTaskFactory.CreateRepairTask(parts: [original]).Value;
        var incoming = PartFactory.CreatePart(id: id, name: "New", cost: 20, quantity: 5).Value;

        var result = task.UpsertParts([incoming]);

        Assert.True(result.IsSuccess);

        var updated = task.Parts.First(p => p.Id == id);
        Assert.Equal("New", updated.Name);
        Assert.Equal(20m, updated.Cost);
        Assert.Equal(5, updated.Quantity);
    }

    [Fact]
    public void UpsertParts_RemovesMissingParts()
    {
        var keep = PartFactory.CreatePart().Value;
        var remove = PartFactory.CreatePart().Value;
        var task = RepairTaskFactory.CreateRepairTask(parts: [keep, remove]).Value;

        var result = task.UpsertParts([keep]);

        Assert.True(result.IsSuccess);
        Assert.Single(task.Parts, keep);
    }

    [Fact]
    public void UpdateRepairTask_ShouldReturnSuccess_WithValidData()
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;

        var result = task.Update("Valid", 123m, RepairDurationInMinutes.Min30);

        Assert.True(result.IsSuccess);
        Assert.Equal("Valid", task.Name);
        Assert.Equal(123m, task.LaborCost);
        Assert.Equal(RepairDurationInMinutes.Min30, task.EstimatedDurationInMins);
    }

    [Theory]
    [InlineData("", 1, RepairDurationInMinutes.Min30, false)]
    [InlineData("  ", 1, RepairDurationInMinutes.Min30, false)]
    [InlineData("Name", 0, RepairDurationInMinutes.Min30, false)]
    [InlineData("Name", 10001, RepairDurationInMinutes.Min30, false)]
    public void UpdateRepairTask_ShouldReturnError_ForInvalidNameOrCost(string name, decimal cost, RepairDurationInMinutes repairDurationInMinutes, bool expected)
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;

        var result = task.Update(name, cost, repairDurationInMinutes);

        Assert.Equal(expected, result.IsSuccess);
    }


    [Fact]
    public void UpdateRepairTask_ShouldReturnError_ForInvalidDuration()
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;
        const RepairDurationInMinutes invalid = (RepairDurationInMinutes)999;

        var result = task.Update("Name", 1m, invalid);

        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.DurationInvalid.Code, result.TopError.Code);
    }
}