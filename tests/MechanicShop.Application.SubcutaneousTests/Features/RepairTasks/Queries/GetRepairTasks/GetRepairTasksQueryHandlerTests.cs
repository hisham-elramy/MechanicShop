using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Tests.Common.RepairTasks;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTasks;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetRepairTasksQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCalled()
    {
        // Arrange
        await ClearCacheAsync();

        // Act
        var result = await _mediator.Send(new GetRepairTasksQuery());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Handle_ShouldIncludeSeededRepairTasks()
    {
        // Arrange
        await ClearCacheAsync();
        var a = await SeedRepairTaskAsync();
        var b = await SeedRepairTaskAsync();

        // Act
        var result = await _mediator.Send(new GetRepairTasksQuery());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Count >= 2);
    }

    [Fact]
    public async Task Handle_ShouldServeCachedList_UntilTagIsRemoved()
    {
        // Arrange
        await ClearCacheAsync();
        await SeedRepairTaskAsync();

        // Act
        var before = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;

        // Assert
        await SeedRepairTaskAsync();
        var cached = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;
        Assert.Equal(before, cached);

        await ClearCacheAsync();
        var fresh = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;
        Assert.True(fresh > before);
    }


    [Fact]
    public async Task Handle_ShouldRefreshList_WhenSingleTaskTagIsRemoved()
    {
        // Arrange
        await ClearCacheAsync();
        await SeedRepairTaskAsync();

        // Act
        var before = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;

        await SeedRepairTaskAsync();
        await factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("repair-task");

        // Assert
        var after = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;
        Assert.True(after > before);
    }


    private async Task ClearCacheAsync()
    {
        var cache = factory.Services.GetRequiredService<HybridCache>();
        await cache.RemoveByTagAsync("repair-tasks");
        await cache.RemoveByTagAsync("repair-task");
    }

    private async Task<RepairTask> SeedRepairTaskAsync()
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;

        _context.RepairTasks.Add(task);
        await _context.SaveChangesAsync(CancellationToken.None);

        return task;
    }
}