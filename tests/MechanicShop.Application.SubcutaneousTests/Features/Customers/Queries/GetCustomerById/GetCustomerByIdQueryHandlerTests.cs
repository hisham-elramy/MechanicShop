using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Queries.GetCustomerById;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers;
using MechanicShop.Tests.Common.Customers;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomerById;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetCustomerByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldReturnMappedCustomer_WhenCustomerExists()
    {
        // Arrange
        var customer = await SeedAsync();

        // Act
        var result = await _mediator.Send(new GetCustomerByIdQuery(customer.Id));

        // Assert
        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(customer.Id, dto.CustomerId);
        Assert.Equal(customer.Name, dto.Name);
        Assert.Equal(customer.PhoneNumber, dto.PhoneNumber);
        Assert.Equal(customer.Email, dto.Email);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllVehicles_WithMappedFields()
    {
        // Arrange
        var customer = await SeedAsync();

        // Act
        var dto = (await _mediator.Send(new GetCustomerByIdQuery(customer.Id))).Value;

        // Assert
        Assert.Equal(customer.Vehicles.Count(), dto.Vehicles.Count);
        foreach (var vehicle in customer.Vehicles)
        {
            var mapped = Assert.Single(dto.Vehicles, v => v.VehicleId == vehicle.Id);
            Assert.Equal(vehicle.Make, mapped.Make);
            Assert.Equal(vehicle.Model, mapped.Model);
            Assert.Equal(vehicle.Year, mapped.Year);
            Assert.Equal(vehicle.LicensePlate, mapped.LicensePlate);
        }
    }

    [Fact]
    public async Task Handle_ShouldNotReturnOtherCustomersVehicles()
    {
        // Arrange
        var customer = await SeedAsync();
        var other = await SeedAsync();

        // Act
        var dto = (await _mediator.Send(new GetCustomerByIdQuery(customer.Id))).Value;

        // Assert
        var otherIds = other.Vehicles.Select(v => v.Id).ToList();
        Assert.DoesNotContain(dto.Vehicles, v => otherIds.Contains(v.VehicleId));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCustomerDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetCustomerByIdQuery(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.CustomerNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldServeCachedResult_UntilCustomerTagIsRemoved()
    {
        // Arrange
        var customer = await SeedAsync();
        var query = new GetCustomerByIdQuery(customer.Id);
        
        // Act & Assert
        Assert.True((await _mediator.Send(query)).IsSuccess);

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.True((await _mediator.Send(query)).IsSuccess);

        await factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("customer");

        var fresh = await _mediator.Send(query);
        Assert.True(fresh.IsError);
        Assert.Equal(ApplicationErrors.CustomerNotFound.Code, fresh.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldNotCacheNotFound_WhenCustomerIsCreatedAfterwards()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        Assert.True((await _mediator.Send(new GetCustomerByIdQuery(id))).IsError);

        await SeedAsync(id);

        Assert.True((await _mediator.Send(new GetCustomerByIdQuery(id))).IsSuccess);
    }


    private async Task<Customer> SeedAsync(Guid? id = null)
    {
        var customer = CustomerFactory.CreateCustomer
        (
            id: id,
            name: $"Customer-{Guid.NewGuid():N}",
            email: $"c-{Guid.NewGuid():N}@example.com"
        ).Value;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(CancellationToken.None);

        return customer;
    }
}