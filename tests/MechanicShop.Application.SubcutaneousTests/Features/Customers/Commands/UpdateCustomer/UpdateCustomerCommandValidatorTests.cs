using MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.UpdateCustomer;

public class UpdateCustomerCommandValidatorTests
{
    private readonly UpdateCustomerCommandValidator _validator = new();

    private static UpdateCustomerCommand Valid()
    {
        return new
        (
            Guid.NewGuid(),
            "John Smith",
            "+15555555555",
            "john@example.com",
            [new UpdateVehicleCommand(null, "Honda", "Accord", 2024, "ABC123")]
        );
    }

    [Fact]
    public void Validate_ShouldFail_WhenCustomerIdIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { CustomerId = Guid.Empty }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.CustomerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenNameIsEmpty(string name)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Name = name }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.Name));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceeds100Characters()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Name = new string('a', 101) }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenEmailIsEmpty(string email)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Email = email }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.Email));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    public void Validate_ShouldFail_WhenEmailIsInvalid(string email)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Email = email }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("1234567890123456")]
    [InlineData("555-555-5555")]
    public void Validate_ShouldFail_WhenPhoneNumberIsInvalid(string phone)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { PhoneNumber = phone }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.PhoneNumber));
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789012345")]
    [InlineData("+15555555555")]
    public void Validate_ShouldPass_WhenPhoneNumberIsValid(string phone)
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(Valid() with { PhoneNumber = phone }).IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehiclesIsNull()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = null! }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.Vehicles));
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehiclesIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [] }).Errors,
            e => e.PropertyName == nameof(UpdateCustomerCommand.Vehicles));
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehicleMakeIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new(null, "", "Accord", 2024, "ABC123")] }).Errors,
            e => e.PropertyName == "Vehicles[0].Make");
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehicleModelIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new(null, "Honda", "", 2024, "ABC123")] }).Errors,
            e => e.PropertyName == "Vehicles[0].Model");
    }

    [Fact]
    public void Validate_ShouldFail_WhenLicensePlateExceeds10Characters()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new(null, "Honda", "Accord", 2024, new string('A', 11))] }).Errors,
            e => e.PropertyName == "Vehicles[0].LicensePlate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(9999)]
    public void Validate_ShouldFail_WhenVehicleYearIsOutOfRange(int year)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new(null, "Honda", "Accord", year, "ABC123")] }).Errors,
            e => e.PropertyName == "Vehicles[0].Year");
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(Valid()).IsValid);
    }
}