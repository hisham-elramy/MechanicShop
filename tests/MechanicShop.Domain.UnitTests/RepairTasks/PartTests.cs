using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Tests.Common.RepairTasks;
using Microsoft.Identity.Client;
using Xunit;

namespace MechanicShop.Domain.UnitTests.RepairTasks;

public class PartTests
{
    [Fact]
    public void CreatePart_ShouldSucceed_WithValidData()
    {
        var id = Guid.NewGuid();
        const string name = "Brake Pad";
        const decimal cost = 100m;
        const int quantity = 2;

        var result = PartFactory.CreatePart(id: id, name: name, cost: cost, quantity: quantity);

        Assert.True(result.IsSuccess);
        
        var part = result.Value;
        Assert.Equal(id, part.Id);
        Assert.IsType<Part>(part);
        Assert.Equal(name, part.Name);
        Assert.Equal(cost, part.Cost);
        Assert.Equal(quantity, part.Quantity);
    }

    [Fact]
    public void CreatePart_ShouldFail_WithInvalidName()
    {
        var result = PartFactory.CreatePart(name: " ");

        Assert.True(result.IsError);
        Assert.Equal(PartErrors.NameRequired.Code, result.TopError.Code);
    }

    [Fact]
    public void CreatePart_ShouldFail_WithInvalidCost()
    {
        var result = PartFactory.CreatePart(cost: 0);

        Assert.True(result.IsError);
        Assert.Equal(PartErrors.CostInvalid.Code, result.TopError.Code);
    }

    [Fact]
    public void CreatePart_ShouldFail_WithInvalidQuantity()
    {
        var result = PartFactory.CreatePart(quantity: 0);

        Assert.True(result.IsError);
        Assert.Equal(PartErrors.QuantityInvalid.Code, result.TopError.Code);
    }

    [Fact]
    public void UpdatePart_ShouldSucceed_WithValidData()
    {
        var part = PartFactory.CreatePart().Value;

        const string name = "Brake Disc";
        const decimal cost = 200m;
        const int quantity = 3;

        var result = part.Update(name, cost, quantity);

        Assert.True(result.IsSuccess);
        Assert.Equal(Result.Updated, result.Value);
        Assert.Equal(name, part.Name);
        Assert.Equal(cost, part.Cost);
        Assert.Equal(quantity, part.Quantity);
    }

    [Fact]
    public void UpdatePart_ShouldFail_WithInvalidName()
    {
        var part = PartFactory.CreatePart().Value;

        const string name = " ";
        const decimal cost = 200m;
        const int quantity = 3;

        var result = part.Update(name, cost, quantity);

        Assert.True(result.IsError);
        Assert.Equal(PartErrors.NameRequired.Code, result.TopError.Code);
    }

    [Fact]
    public void UpdatePart_ShouldFail_WithInvalidCost()
    {
        var part = PartFactory.CreatePart().Value;

        const string name = "Brake Disc";
        const decimal cost = 0m;
        const int quantity = 3;

        var result = part.Update(name, cost, quantity);

        Assert.True(result.IsError);
        Assert.Equal(PartErrors.CostInvalid.Code, result.TopError.Code);
    }

    [Fact]
    public void UpdatePart_ShouldFail_WithInvalidQuantity()
    {
        var part = PartFactory.CreatePart().Value;

        const string name = "Brake Disc";
        const decimal cost = 200m;
        const int quantity = 0;

        var result = part.Update(name, cost, quantity);

        Assert.True(result.IsError);
        Assert.Equal(PartErrors.QuantityInvalid.Code, result.TopError.Code);
    }
}