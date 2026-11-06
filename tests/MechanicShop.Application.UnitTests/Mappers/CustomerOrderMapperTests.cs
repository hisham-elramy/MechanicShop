using MechanicShop.Application.Features.Customers.Mappers;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Tests.Common.Customers;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class CustomerMapperTests
{
    [Fact]
    public void ToDto_Customer_ShouldMapCorrectly()
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle().Value;
        var customer = CustomerFactory.CreateCustomer(vehicles: [vehicle]).Value;

        // Act
        var dto = customer.ToDto();

        // Assert
        Assert.Equal(customer.Id, dto.CustomerId);
        Assert.Equal(customer.Name, dto.Name);
        Assert.Equal(customer.Email, dto.Email);
        Assert.Equal(customer.PhoneNumber, dto.PhoneNumber);

        var vehicleDto = Assert.Single(dto.Vehicles);
        Assert.Equal(vehicle.Id, vehicleDto.VehicleId);
        Assert.Equal(vehicle.Make, vehicleDto.Make);
        Assert.Equal(vehicle.Model, vehicleDto.Model);
        Assert.Equal(vehicle.Year, vehicleDto.Year);
        Assert.Equal(vehicle.LicensePlate, vehicleDto.LicensePlate);
    }

    [Fact]
    public void ToDto_Customer_ShouldMapAllVehicles()
    {
        // Arrange
        var vehicle1 = VehicleFactory.CreateVehicle(id: Guid.NewGuid()).Value;
        var vehicle2 = VehicleFactory.CreateVehicle(id: Guid.NewGuid()).Value;
        var customer = CustomerFactory.CreateCustomer(vehicles: [vehicle1, vehicle2]).Value;

        // Act
        var dto = customer.ToDto();

        // Assert
        Assert.Equal(2, dto.Vehicles.Count);
        Assert.Contains(dto.Vehicles, v => v.VehicleId == vehicle1.Id);
        Assert.Contains(dto.Vehicles, v => v.VehicleId == vehicle2.Id);
    }

    [Fact]
    public void ToDto_Customer_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        Customer customer = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => customer.ToDto());
    }

    [Fact]
    public void ToDto_Vehicle_ShouldMapCorrectly()
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle().Value;

        // Act
        var dto = vehicle.ToDto();

        // Assert
        Assert.Equal(vehicle.Id, dto.VehicleId);
        Assert.Equal(vehicle.Make, dto.Make);
        Assert.Equal(vehicle.Model, dto.Model);
        Assert.Equal(vehicle.Year, dto.Year);
        Assert.Equal(vehicle.LicensePlate, dto.LicensePlate);
    }

    [Fact]
    public void ToDto_Vehicle_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        Vehicle vehicle = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => vehicle.ToDto());
    }

    [Fact]
    public void ToDtos_Customers_ShouldMapListCorrectly()
    {
        // Arrange
        var customer1 = CustomerFactory.CreateCustomer(id: Guid.NewGuid()).Value;
        var customer2 = CustomerFactory.CreateCustomer(id: Guid.NewGuid()).Value;
        var customers = new List<Customer> { customer1, customer2 };

        // Act
        var dtos = customers.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(customer1.Id, dtos[0].CustomerId);
        Assert.Equal(customer2.Id, dtos[1].CustomerId);
        Assert.Equal(customer1.Name, dtos[0].Name);
        Assert.Equal(customer2.Name, dtos[1].Name);
    }

    [Fact]
    public void ToDtos_Customers_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var customers = new List<Customer>();

        // Act
        var dtos = customers.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public void ToDtos_Customers_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<Customer> customers = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => customers.ToDtos());
    }

    [Fact]
    public void ToDtos_Vehicles_ShouldMapListCorrectly()
    {
        // Arrange
        var vehicle1 = VehicleFactory.CreateVehicle(id: Guid.NewGuid()).Value;
        var vehicle2 = VehicleFactory.CreateVehicle(id: Guid.NewGuid()).Value;
        var vehicles = new List<Vehicle> { vehicle1, vehicle2 };

        // Act
        var dtos = vehicles.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(vehicle1.Id, dtos[0].VehicleId);
        Assert.Equal(vehicle2.Id, dtos[1].VehicleId);
        Assert.Equal(vehicle1.LicensePlate, dtos[0].LicensePlate);
        Assert.Equal(vehicle2.LicensePlate, dtos[1].LicensePlate);
    }

    [Fact]
    public void ToDtos_Vehicles_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var vehicles = new List<Vehicle>();

        // Act
        var dtos = vehicles.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }
}