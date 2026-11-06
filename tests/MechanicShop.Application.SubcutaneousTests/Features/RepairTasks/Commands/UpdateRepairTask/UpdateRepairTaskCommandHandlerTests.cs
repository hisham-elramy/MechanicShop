using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Commands.UpdateRepairTask;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Tests.Common.RepairTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.UpdateRepairTask;

[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateRepairTaskCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();


    [Fact]
    public async Task Handle_ShouldUpdateScalarFields_WhenCommandIsValid()
    {
        // Arrange
        var task = await SeedAsync();
        var newName = UniqueName();

        // Act
        var result = await _mediator.Send(Command(task) with
        {
            Name = newName, LaborCost = 250m, EstimatedDurationInMins = RepairDurationInMinutes.Min60
        });

        // Assert
        Assert.True(result.IsSuccess);
        var saved = await ReloadAsync(task.Id);
        Assert.Equal(newName, saved.Name);
        Assert.Equal(250m, saved.LaborCost);
        Assert.Equal(RepairDurationInMinutes.Min60, saved.EstimatedDurationInMins);
    }

    [Fact]
    public async Task Handle_ShouldKeepPartId_WhenExistingPartIsChanged()
    {
        // Arrange
        var task = await SeedAsync();
        var part = task.Parts.Single();

        // Act
        var result = await _mediator.Send(Command(task) with
        {
            Parts = [new UpdateRepairTaskPartCommand(part.Id, "Renamed", 77m, 3)]
        });

        // Assert
        Assert.True(result.IsSuccess);
        var saved = Assert.Single((await ReloadAsync(task.Id)).Parts);
        Assert.Equal(part.Id, saved.Id);
        Assert.Equal("Renamed", saved.Name);
        Assert.Equal(77m, saved.Cost);
        Assert.Equal(3, saved.Quantity);
    }

    [Fact]
    public async Task Handle_ShouldAddNewPart_WhenPartIdIsNull()
    {
        // Arrange
        var task = await SeedAsync();
        var existing = task.Parts.Single();

        // Act
        var result = await _mediator.Send(Command(task) with
        {
            Parts =
            [
                new UpdateRepairTaskPartCommand(existing.Id, existing.Name!, existing.Cost, existing.Quantity),
                new UpdateRepairTaskPartCommand(null, "New Part", 40m, 1),
            ]
        });

        // Arrange
        Assert.True(result.IsSuccess);
        var parts = (await ReloadAsync(task.Id)).Parts.ToList();
        Assert.Equal(2, parts.Count);
        Assert.Contains(parts, p => p.Name == "New Part" && p.Id != Guid.Empty);
    }

    [Fact]
    public async Task Handle_ShouldRemoveOmittedParts()
    {
        // Arrange
        var task = await SeedAsync(parts: [PartFactory.CreatePart(name: "Keep").Value, PartFactory.CreatePart(name: "Drop").Value]);
        var keep = task.Parts.Single(p => p.Name == "Keep");

        // Act
        var result = await _mediator.Send(Command(task) with
        {
            Parts = [new UpdateRepairTaskPartCommand(keep.Id, keep.Name!, keep.Cost, keep.Quantity)]
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Keep", Assert.Single((await ReloadAsync(task.Id)).Parts).Name);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNameIsUnchanged()
    {
        // Arrange
        var task = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(task));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldTrimName()
    {
        // Arrange
        var task = await SeedAsync();
        var name = UniqueName();

        // Act
        await _mediator.Send(Command(task) with { Name = $"  {name}  " });

        // Assert
        Assert.Equal(name, (await ReloadAsync(task.Id)).Name);
    }

    [Fact]
    public async Task Handle_ShouldNotChangeOtherTasks()
    {
        // Arrange
        var target = await SeedAsync();
        var other = await SeedAsync();

        // Act
        await _mediator.Send(Command(target) with { Name = UniqueName() });

        // Assert
        Assert.Equal(other.Name, (await ReloadAsync(other.Id)).Name);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDoesNotExist()
    {
        // Arrange
        var task = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(task) with { RepairTaskId = Guid.NewGuid() });

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.RepairTaskNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNameBelongsToAnotherTask()
    {
        // Arrange
        var first = await SeedAsync();
        var second = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(second) with { Name = first.Name });

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.DuplicateName.Code, result.TopError.Code);
        Assert.Equal(second.Name, (await ReloadAsync(second.Id)).Name);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPartIdBelongsToAnotherTask()
    {
        // Arrange
        var task = await SeedAsync();
        var other = await SeedAsync();
        var foreign = other.Parts.Single();

        // Act
        var result = await _mediator.Send(Command(task) with
        {
            Parts = [new UpdateRepairTaskPartCommand(foreign.Id, "Stolen", 10m, 1)]
        });

        // Assert
        Assert.True(result.IsError);
        Assert.Single((await ReloadAsync(other.Id)).Parts);
    }

    [Fact]
    public async Task Handle_ShouldFailAndChangeNothing_WhenAPartIsInvalid()
    {
        // Arrange
        var task = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(task) with
        {
            Name = UniqueName(),
            Parts = [new UpdateRepairTaskPartCommand(null, "Bad", 0m, 1)]
        });

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(task.Name, (await ReloadAsync(task.Id)).Name);
    }

    [Fact]
    public async Task Handle_ShouldRefreshSingleTaskQuery_WhenTaskIsUpdated()
    {
        // Arrange
        var task = await SeedAsync();
        
        // Act & Assert
        Assert.True((await _mediator.Send(new GetRepairTaskByIdQuery(task.Id))).IsSuccess);   // cached
        var newName = UniqueName();

        await _mediator.Send(Command(task) with { Name = newName });

        Assert.Equal(newName, (await _mediator.Send(new GetRepairTaskByIdQuery(task.Id))).Value.Name);
    }

    [Fact]
    public async Task Handle_ShouldRefreshListQuery_WhenTaskIsUpdated()
    {
        // Arrange
        var task = await SeedAsync();

        // Act & Assert
        await factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("repair-tasks");
        Assert.True((await _mediator.Send(new GetRepairTasksQuery())).IsSuccess);
        var newName = UniqueName();

        await _mediator.Send(Command(task) with { Name = newName });

        var list = (await _mediator.Send(new GetRepairTasksQuery())).Value;
        Assert.Contains(list, t => t.Name == newName);
    }


    private static string UniqueName()
    {
        return $"Task-{Guid.NewGuid():N}";
    }

    private static UpdateRepairTaskCommand Command(RepairTask task)
    {
        return new
        (
            task.Id,
            task.Name,
            task.LaborCost,
            task.EstimatedDurationInMins,
            [.. task.Parts.Select(p => new UpdateRepairTaskPartCommand(p.Id, p.Name!, p.Cost, p.Quantity))]
        );
    }

    private async Task<RepairTask> SeedAsync(List<Part>? parts = null)
    {
        var task = RepairTaskFactory.CreateRepairTask(name: UniqueName(), parts: parts).Value;

        _context.RepairTasks.Add(task);
        await _context.SaveChangesAsync(CancellationToken.None);

        return task;
    }

    private Task<RepairTask> ReloadAsync(Guid id)
    {
        return factory.CreateAppDbContext().RepairTasks
            .AsNoTracking()
            .Include(rt => rt.Parts)
            .SingleAsync(rt => rt.Id == id);
    }
}