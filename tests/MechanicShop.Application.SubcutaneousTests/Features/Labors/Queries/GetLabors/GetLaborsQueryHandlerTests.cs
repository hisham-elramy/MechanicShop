using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Labors.Queries.GetLabors;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Employees;
using MechanicShop.Tests.Common.Employees;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Labors.Queries.GetLabors;


[Collection(WebAppFactoryCollection.CollectionName)]
public class GetLaborsQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCalled()
    {
        // Arrange
        await ClearCacheAsync();

        // Act
        var result = await _mediator.Send(new GetLaborsQuery());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Handle_ShouldIncludeNewLabor()
    {
        // Arrange
        await ClearCacheAsync();

        // Act & Assert
        var before = (await _mediator.Send(new GetLaborsQuery())).Value.Count;
        await ClearCacheAsync();

        await SeedAsync(EmployeeFactory.CreateLabor().Value);

        var after = (await _mediator.Send(new GetLaborsQuery())).Value.Count;
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task Handle_ShouldExcludeManagers()
    {
        // Arrange
        await ClearCacheAsync();
        
        // Act & Assert
        var before = (await _mediator.Send(new GetLaborsQuery())).Value.Count;
        await ClearCacheAsync();

        await SeedAsync(EmployeeFactory.CreateManager().Value);

        var after = (await _mediator.Send(new GetLaborsQuery())).Value.Count;
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyLabors_WhenBothRolesAreSeeded()
    {
        // Arrange
        await ClearCacheAsync();

        // Act & Assert
        var before = (await _mediator.Send(new GetLaborsQuery())).Value.Count;
        await ClearCacheAsync();

        await SeedAsync(EmployeeFactory.CreateLabor().Value);
        await SeedAsync(EmployeeFactory.CreateManager().Value);
        await SeedAsync(EmployeeFactory.CreateLabor().Value);

        var after = (await _mediator.Send(new GetLaborsQuery())).Value.Count;
        Assert.Equal(before + 2, after);
    }

    [Fact]
    public async Task Handle_ShouldServeCachedList_UntilTagIsRemoved()
    {
        // Arrange
        await ClearCacheAsync();

        // Act & Assert
        var first = (await _mediator.Send(new GetLaborsQuery())).Value.Count;

        await SeedAsync(EmployeeFactory.CreateLabor().Value);
        Assert.Equal(first, (await _mediator.Send(new GetLaborsQuery())).Value.Count);

        await ClearCacheAsync();
        Assert.Equal(first + 1, (await _mediator.Send(new GetLaborsQuery())).Value.Count);
    }


    private Task ClearCacheAsync()
    {
        return factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("labors").AsTask();
    }

    private async Task SeedAsync(Employee employee)
    {
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(CancellationToken.None);
    }
}