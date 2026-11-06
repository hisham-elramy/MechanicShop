using MechanicShop.Application.Features.RepairTasks.Commands.CreateRepairTask;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.CreateRepairTask;

[Collection(WebAppFactoryCollection.CollectionName)]
public class CreateRepairTaskCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();


    [Fact]
    public async Task Handle_ShouldSucceedAndPersist_WhenCommandIsValid()
    {
        // Arrange
        var name = UniqueName();

        // Act
        var result = await _mediator.Send(Command(name));

        // Assert
        Assert.True(result.IsSuccess);

        var saved = await FindByNameAsync(name);
        Assert.NotNull(saved);
        Assert.Equal(100m, saved.LaborCost);
        Assert.Equal(RepairDurationInMinutes.Min30, saved.EstimatedDurationInMins);

        var part = Assert.Single(saved.Parts);
        Assert.Equal("Brake Pad", part.Name);
        Assert.Equal(50m, part.Cost);
        Assert.Equal(2, part.Quantity);
    }

    [Fact]
    public async Task Handle_ShouldPersistAllParts_WhenSeveralPartsAreGiven()
    {
        // Arrange
        var name = UniqueName();
        var command = Command(name) with
        {
            Parts =
            [
                new CreateRepairTaskPartCommand("Pad", 50m, 2),
                new CreateRepairTaskPartCommand("Rotor", 120m, 1),
                new CreateRepairTaskPartCommand("Fluid", 15m, 3),
            ]
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, (await FindByNameAsync(name))!.Parts.Count());
    }

    [Fact]
    public async Task Handle_ShouldTrimName_WhenNameHasSurroundingSpaces()
    {
        // Arrange
        var name = UniqueName();

        // Act
        var result = await _mediator.Send(Command($"  {name}  "));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(await FindByNameAsync(name));
        Assert.Equal(name, result.Value.Name);
    }

    [Fact]
    public async Task Handle_ShouldReturnDtoMatchingPersistedTask()
    {
        // Arrange
        var name = UniqueName();

        // Act
        var result = await _mediator.Send(Command(name));

        // Assert
        Assert.True(result.IsSuccess);
        var dto = result.Value;
        var saved = await FindByNameAsync(name);

        Assert.NotNull(saved);
        Assert.Equal(saved.Id, dto.RepairTaskId);
        Assert.Equal(name, dto.Name);
        Assert.Equal(RepairDurationInMinutes.Min30, dto.EstimatedDurationInMins);
        Assert.Equal(100m, dto.LaborCost);

        var part = Assert.Single(dto.Parts);
        Assert.NotEqual(Guid.Empty, part.PartId);
        Assert.Equal("Brake Pad", part.Name);
        Assert.Equal(50m, part.Cost);
        Assert.Equal(2, part.Quantity);
        Assert.Equal(saved.Parts.Single().Id, part.PartId);
    }

    [Fact]
    public async Task Handle_ShouldCalculateTotalCost_AsLaborPlusPartsTimesQuantity()
    {
        // Arrange
        var command = Command(UniqueName()) with
        {
            LaborCost = 100m,
            Parts =
            [
                new CreateRepairTaskPartCommand("Pad", 50m, 2),
                new CreateRepairTaskPartCommand("Rotor", 120m, 1),
            ]
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.Equal(320m, result.Value.TotalCost);
    }

    [Fact]
    public async Task Handle_ShouldReturnDistinctPartIds_WhenSeveralPartsAreGiven()
    {
        // Arrange
        var command = Command(UniqueName()) with
        {
            Parts =
            [
                new CreateRepairTaskPartCommand("Pad", 50m, 2),
                new CreateRepairTaskPartCommand("Rotor", 120m, 1),
            ]
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        var ids = result.Value.Parts.Select(p => p.PartId).ToList();
        Assert.Equal(2, ids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, ids);
    }

    [Fact]
    public async Task Handle_ShouldReturnPartsInRequestOrder()
    {
        // Arrange
        var command = Command(UniqueName()) with
        {
            Parts =
            [
                new CreateRepairTaskPartCommand("First", 10m, 1),
                new CreateRepairTaskPartCommand("Second", 20m, 1),
                new CreateRepairTaskPartCommand("Third", 30m, 1),
            ]
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.Equal(new[] { "First", "Second", "Third" }, result.Value.Parts.Select(p => p.Name));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNameAlreadyExists()
    {
        // Arrange
        var name = UniqueName();

        // Act & Assert
        Assert.True((await _mediator.Send(Command(name))).IsSuccess);
        var result = await _mediator.Send(Command(name));

        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.DuplicateName.Code, result.TopError.Code);
        Assert.Equal(1, await CountByNameAsync(name));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNameDiffersOnlyByCase()
    {
        // Arrange
        var name = UniqueName();

        // Act & Assert
        Assert.True((await _mediator.Send(Command(name))).IsSuccess);

        var result = await _mediator.Send(Command(name.ToUpperInvariant()));

        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.DuplicateName.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNameContainsWildcardMatchingAnotherTask()
    {
        // Arrange
        var prefix = UniqueName();

        // Act & Assert
        Assert.True((await _mediator.Send(Command($"{prefix}-A"))).IsSuccess);

        var result = await _mediator.Send(Command($"{prefix}%"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNameContainsUnderscoreMatchingAnotherTask()
    {
        // Arrange
        var prefix = UniqueName();

        // Act & Assert
        Assert.True((await _mediator.Send(Command($"{prefix}-A"))).IsSuccess);
        var result = await _mediator.Send(Command($"{prefix}_A"));

        Assert.True(result.IsSuccess);
    }


    [Fact]
    public async Task Handle_ShouldFailAndPersistNothing_WhenAPartIsInvalid()
    {
        // Arrange
        var name = UniqueName();
        var command = Command(name) with { Parts = [new CreateRepairTaskPartCommand("Pad", 0m, 2)] };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsError);
        Assert.Null(await FindByNameAsync(name));
    }

    [Fact]
    public async Task Handle_ShouldFailAndPersistNothing_WhenDurationIsMissing()
    {
        // Arrange
        var name = UniqueName();

        // Act
        var result = await _mediator.Send(Command(name) with { EstimatedDurationInMins = null });

        // Assert
        Assert.True(result.IsError);
        Assert.Null(await FindByNameAsync(name));
    }

    [Fact]
    public async Task Handle_ShouldFailAndPersistNothing_WhenOneOfSeveralPartsIsInvalid()
    {
        // Arrange
        var name = UniqueName();
        var command = Command(name) with
        {
            Parts =
            [
                new CreateRepairTaskPartCommand("Pad", 50m, 2),
                new CreateRepairTaskPartCommand("Rotor", 0m, 1),
            ]
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsError);
        Assert.Null(await FindByNameAsync(name));
    }


    [Fact]
    public async Task Handle_ShouldRefreshRepairTaskList_WhenTaskIsCreated()
    {
        // Arrange
        var cache = factory.Services.GetRequiredService<HybridCache>();
        await cache.RemoveByTagAsync("repair-tasks");

        // Act & Assert
        var before = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;

        Assert.True((await _mediator.Send(Command(UniqueName()))).IsSuccess);

        var after = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;
        Assert.True(after > before);
    }

    private static string UniqueName()
    {
        return $"Task-{Guid.NewGuid():N}";
    }

    private static CreateRepairTaskCommand Command(string name)
    {
        return new
        (
            Name: name,
            LaborCost: 100m,
            EstimatedDurationInMins: RepairDurationInMinutes.Min30,
            Parts: [new CreateRepairTaskPartCommand("Brake Pad", 50m, 2)]
        );
    }

    private Task<RepairTask?> FindByNameAsync(string name)
    {
        return factory.CreateAppDbContext().RepairTasks
            .AsNoTracking()
            .Include(rt => rt.Parts)
            .SingleOrDefaultAsync(rt => rt.Name == name);
    }

    private Task<int> CountByNameAsync(string name)
    {
        return factory.CreateAppDbContext().RepairTasks.CountAsync(rt => rt.Name == name);
    }

}