using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Queries.GetCustomers;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers;
using MechanicShop.Tests.Common.Customers;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomers;


[Collection(WebAppFactoryCollection.CollectionName)]
public class GetCustomersQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCalled()
    {
        // Arrange
        await ClearCacheAsync();

        // Act
        var result = await _mediator.Send(new GetCustomersQuery());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Handle_ShouldIncludeSeededCustomers_WithMappedFields()
    {
        // Arrange
        await ClearCacheAsync();
        var customer = await SeedAsync();
        await ClearCacheAsync();

        // Act
        var result = await _mediator.Send(new GetCustomersQuery());

        // Assert
        var dto = Assert.Single(result.Value, c => c.CustomerId == customer.Id);
        Assert.Equal(customer.Name, dto.Name);
        Assert.Equal(customer.PhoneNumber, dto.PhoneNumber);
        Assert.Equal(customer.Email, dto.Email);
    }

    [Fact]
    public async Task Handle_ShouldIncludeEachCustomersVehicles()
    {
        // Arrange
        await ClearCacheAsync();
        var customer = await SeedAsync();
        await ClearCacheAsync();

        // Act
        var result = await _mediator.Send(new GetCustomersQuery());

        // Assert
        var dto = Assert.Single(result.Value, c => c.CustomerId == customer.Id);
        Assert.Equal(customer.Vehicles.Count(), dto.Vehicles.Count);
        Assert.All(customer.Vehicles, v => Assert.Contains(dto.Vehicles, m => m.VehicleId == v.Id));
    }

    [Fact]
    public async Task Handle_ShouldReturnEachCustomerOnce_DespiteMultipleVehicles()
    {
        // Arrange
        await ClearCacheAsync();
        var customer = await SeedAsync();
        await ClearCacheAsync();

        // Act
        var result = await _mediator.Send(new GetCustomersQuery());

        // Assert
        Assert.Single(result.Value, c => c.CustomerId == customer.Id);
    }

    [Fact]
    public async Task Handle_ShouldIncludeNewCustomer_WhenCacheIsCleared()
    {
        // Arrange
        await ClearCacheAsync();

        // Act
        var before = (await _mediator.Send(new GetCustomersQuery())).Value.Count;
        await ClearCacheAsync();

        await SeedAsync();

        // Assert
        Assert.Equal(before + 1, (await _mediator.Send(new GetCustomersQuery())).Value.Count);
    }

    [Fact]
    public async Task Handle_ShouldSortByName()
    {
        // Arrange
        await ClearCacheAsync();
        var prefix = $"Sort-{Guid.NewGuid():N}";
        await SeedAsync(name: $"{prefix}-C");
        await SeedAsync(name: $"{prefix}-A");
        await SeedAsync(name: $"{prefix}-B");
        await ClearCacheAsync();

        // Act
        var result = await _mediator.Send(new GetCustomersQuery());

        // Assert
        var names = result.Value.Where(c => c.Name.StartsWith(prefix)).Select(c => c.Name).ToList();
        Assert.Equal(new[] { $"{prefix}-A", $"{prefix}-B", $"{prefix}-C" }, names);
    }

    [Fact]
    public async Task Handle_ShouldServeCachedList_UntilTagIsRemoved()
    {
        // Arrange
        await ClearCacheAsync();

        // Act
        var first = (await _mediator.Send(new GetCustomersQuery())).Value.Count;

        // Assert
        await SeedAsync();
        Assert.Equal(first, (await _mediator.Send(new GetCustomersQuery())).Value.Count);

        await ClearCacheAsync();
        Assert.Equal(first + 1, (await _mediator.Send(new GetCustomersQuery())).Value.Count);
    }


    private Task ClearCacheAsync()
    {
        return factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("customer").AsTask();
    }

    private async Task<Customer> SeedAsync(string? name = null)
    {
        var customer = CustomerFactory.CreateCustomer
        (
            name: name ?? $"Customer-{Guid.NewGuid():N}",
            email: $"c-{Guid.NewGuid():N}@example.com"
        ).Value;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(CancellationToken.None);

        return customer;
    }
}