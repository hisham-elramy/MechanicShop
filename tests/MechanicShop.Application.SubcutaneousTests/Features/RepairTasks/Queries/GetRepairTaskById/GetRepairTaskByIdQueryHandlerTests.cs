using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Tests.Common.RepairTasks;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTaskById;


[Collection(WebAppFactoryCollection.CollectionName)]
public class GetRepairTaskByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenRepairTaskExists()
    {
        // Arrange
        var task = await SeedRepairTaskAsync();

        // Act
        var result = await _mediator.Send(new GetRepairTaskByIdQuery(task.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRepairTaskDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetRepairTaskByIdQuery(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.RepairTaskNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenRepairTaskHasNoParts()
    {
        // Arrange
        var task = await SeedRepairTaskAsync(parts: []);

        // Act
        var result = await _mediator.Send(new GetRepairTaskByIdQuery(task.Id));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldServeCachedResult_UntilRepairTaskTagIsRemoved()
    {
        // Arrange
        var task = await SeedRepairTaskAsync();

        // Act
        var query = new GetRepairTaskByIdQuery(task.Id);

        // Assert
        Assert.True((await _mediator.Send(query)).IsSuccess);

        _context.RepairTasks.Remove(task);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.True((await _mediator.Send(query)).IsSuccess);   // still cached

        await factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("repair-task");

        var fresh = await _mediator.Send(query);
        Assert.True(fresh.IsError);
        Assert.Equal(ApplicationErrors.RepairTaskNotFound.Code, fresh.TopError.Code);
    }


    [Fact]
    public async Task Handle_ShouldNotCacheNotFound_WhenRepairTaskIsCreatedAfterwards()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        Assert.True((await _mediator.Send(new GetRepairTaskByIdQuery(id))).IsError);

        await SeedRepairTaskAsync(id: id);

        Assert.True((await _mediator.Send(new GetRepairTaskByIdQuery(id))).IsSuccess);
    }



    private async Task<RepairTask> SeedRepairTaskAsync(Guid? id = null, List<MechanicShop.Domain.RepairTasks.Parts.Part>? parts = null)
    {
        var task = RepairTaskFactory.CreateRepairTask(id: id, parts: parts).Value;

        _context.RepairTasks.Add(task);
        await _context.SaveChangesAsync(CancellationToken.None);

        return task;
    }
}