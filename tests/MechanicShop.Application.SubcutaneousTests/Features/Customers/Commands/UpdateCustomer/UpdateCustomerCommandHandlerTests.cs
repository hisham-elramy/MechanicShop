using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers;
using MechanicShop.Tests.Common.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.UpdateCustomer;


[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateCustomerCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();


    [Fact]
    public async Task Handle_ShouldUpdateScalarFields_WhenCommandIsValid()
    {
        // Arrange
        var customer = await SeedAsync();
        var newEmail = UniqueEmail();

        // Act
        var result = await _mediator.Send(Command(customer) with
        {
            Name = "New Name", PhoneNumber = "+19998887777", Email = newEmail
        });

        // Assert
        Assert.True(result.IsSuccess);
        var saved = await ReloadAsync(customer.Id);
        Assert.Equal("New Name", saved.Name);
        Assert.Equal("+19998887777", saved.PhoneNumber);
        Assert.Equal(newEmail, saved.Email);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNothingChanges()
    {
        // Arrange
        var customer = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(customer));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldKeepVehicleId_WhenExistingVehicleIsChanged()
    {
        // Arrange
        var customer = await SeedAsync();
        var vehicle = customer.Vehicles.First();
        var others = customer.Vehicles.Skip(1)
            .Select(v => new UpdateVehicleCommand(v.Id, v.Make, v.Model, v.Year, v.LicensePlate));

        // Act
        var result = await _mediator.Send(Command(customer) with
        {
            Vehicles = [new UpdateVehicleCommand(vehicle.Id, "Tesla", "Model 3", 2023, "NEW999"), .. others]
        });

        // Assert
        Assert.True(result.IsSuccess);
        var saved = (await ReloadAsync(customer.Id)).Vehicles.Single(v => v.Id == vehicle.Id);
        Assert.Equal("Tesla", saved.Make);
        Assert.Equal("Model 3", saved.Model);
        Assert.Equal(2023, saved.Year);
        Assert.Equal("NEW999", saved.LicensePlate);
    }

    [Fact]
    public async Task Handle_ShouldAddNewVehicle_WhenVehicleIdIsNull()
    {
        // Arrange
        var customer = await SeedAsync();
        var existing = customer.Vehicles
            .Select(v => new UpdateVehicleCommand(v.Id, v.Make, v.Model, v.Year, v.LicensePlate));

        // Act
        var result = await _mediator.Send(Command(customer) with
        {
            Vehicles = [.. existing, new UpdateVehicleCommand(null, "Ford", "Focus", 2020, "FRD123")]
        });

        // Assert
        Assert.True(result.IsSuccess);
        var vehicles = (await ReloadAsync(customer.Id)).Vehicles.ToList();
        Assert.Equal(customer.Vehicles.Count() + 1, vehicles.Count);
        Assert.Contains(vehicles, v => v.Make == "Ford" && v.Id != Guid.Empty);
    }

    [Fact]
    public async Task Handle_ShouldRemoveOmittedVehicles()
    {
        // Arrange
        var customer = await SeedAsync();
        var keep = customer.Vehicles.First();

        // Act
        var result = await _mediator.Send(Command(customer) with
        {
            Vehicles = [new UpdateVehicleCommand(keep.Id, keep.Make, keep.Model, keep.Year, keep.LicensePlate)]
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(keep.Id, Assert.Single((await ReloadAsync(customer.Id)).Vehicles).Id);
    }

    [Fact]
    public async Task Handle_ShouldStoreEmailLowercasedAndTrimmed()
    {
        // Arrange
        var customer = await SeedAsync();
        var email = UniqueEmail();

        // Act
        await _mediator.Send(Command(customer) with { Email = $"  {email.ToUpperInvariant()}  " });

        // Assert
        Assert.Equal(email, (await ReloadAsync(customer.Id)).Email);
    }

    [Fact]
    public async Task Handle_ShouldNotChangeOtherCustomers()
    {
        // Arrange
        var target = await SeedAsync();
        var other = await SeedAsync();

        // Act
        await _mediator.Send(Command(target) with { Name = "Changed" });

        // Assert
        Assert.Equal(other.Name, (await ReloadAsync(other.Id)).Name);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCustomerDoesNotExist()
    {
        // Arrange
        var customer = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(customer) with { CustomerId = Guid.NewGuid() });

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.CustomerNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmailBelongsToAnotherCustomer()
    {
        // Arrange
        var first = await SeedAsync();
        var second = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(second) with { Email = first.Email! });

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(CustomerErrors.CustomerExists.Code, result.TopError.Code);
        Assert.Equal(second.Email, (await ReloadAsync(second.Id)).Email);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenVehicleIdBelongsToAnotherCustomer()
    {
        // Arrange
        var customer = await SeedAsync();
        var other = await SeedAsync();
        var foreign = other.Vehicles.First();

        // Act
        var result = await _mediator.Send(Command(customer) with
        {
            Vehicles = [new UpdateVehicleCommand(foreign.Id, "Stolen", "Car", 2020, "STL000")]
        });

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(other.Vehicles.Count(), (await ReloadAsync(other.Id)).Vehicles.Count());
    }

    [Fact]
    public async Task Handle_ShouldFailAndChangeNothing_WhenAVehicleIsInvalid()
    {
        // Arrange
        var customer = await SeedAsync();

        // Act
        var result = await _mediator.Send(Command(customer) with
        {
            Name = "Should Not Persist",
            Vehicles = [new UpdateVehicleCommand(null, "", "Accord", 2024, "ABC123")]
        });

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(customer.Name, (await ReloadAsync(customer.Id)).Name);
    }


    private static string UniqueEmail()
    {
        return $"c-{Guid.NewGuid():N}@example.com";
    }

    private static UpdateCustomerCommand Command(Customer c)
    {
        return new
        (
            c.Id,
            c.Name!,
            c.PhoneNumber!,
            c.Email!,
            [.. c.Vehicles.Select(v => new UpdateVehicleCommand(v.Id, v.Make, v.Model, v.Year, v.LicensePlate))]
        );
    }

    private async Task<Customer> SeedAsync()
    {
        var customer = CustomerFactory.CreateCustomer(email: UniqueEmail()).Value;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(CancellationToken.None);

        return customer;
    }

    private Task<Customer> ReloadAsync(Guid id)
    {
        return factory.CreateAppDbContext().Customers
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .SingleAsync(c => c.Id == id);
    }
}