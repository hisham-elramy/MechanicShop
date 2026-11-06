using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.CreateCustomer;

public class CreateCustomerCommandValidatorTests
{
    private readonly CreateCustomerCommandValidator _validator = new();

    private static CreateCustomerCommand Valid()
    {
        return new
        (
            "John Smith",
            "+15555555555",
            "john@example.com",
            [new CreateVehicleCommand("Honda", "Accord", 2024, "ABC123")]
        );
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenNameIsEmpty(string name)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Name = name }).Errors,
            e => e.PropertyName == nameof(CreateCustomerCommand.Name));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceeds100Characters()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Name = new string('a', 101) }).Errors,
            e => e.PropertyName == nameof(CreateCustomerCommand.Name));
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenEmailIsEmpty(string email)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Email = email }).Errors,
            e => e.PropertyName == nameof(CreateCustomerCommand.Email));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@missing.com")]
    public void Validate_ShouldFail_WhenEmailIsInvalid(string email)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Email = email }).Errors,
            e => e.PropertyName == nameof(CreateCustomerCommand.Email));
    }

    [Fact]
    public void Validate_ShouldFail_WhenEmailExceeds100Characters()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Email = new string('a', 95) + "@x.com" }).Errors,
            e => e.PropertyName == nameof(CreateCustomerCommand.Email));
    }


    [Theory]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("1234567890123456")]
    [InlineData("555-555-5555")]
    [InlineData("abc1234567")]
    [InlineData("++15555555")]
    public void Validate_ShouldFail_WhenPhoneNumberIsInvalid(string phone)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { PhoneNumber = phone }).Errors,
            e => e.PropertyName == nameof(CreateCustomerCommand.PhoneNumber));
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
            e => e.PropertyName == nameof(CreateCustomerCommand.Vehicles));
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehiclesIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [] }).Errors,
            e => e.PropertyName == nameof(CreateCustomerCommand.Vehicles));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenVehicleMakeIsEmpty(string make)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new(make, "Accord", 2024, "ABC123")] }).Errors,
            e => e.PropertyName == "Vehicles[0].Make");
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehicleModelIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new("Honda", "", 2024, "ABC123")] }).Errors,
            e => e.PropertyName == "Vehicles[0].Model");
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehicleMakeExceeds50Characters()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new(new string('a', 51), "Accord", 2024, "ABC123")] }).Errors,
            e => e.PropertyName == "Vehicles[0].Make");
    }

    [Fact]
    public void Validate_ShouldFail_WhenLicensePlateIsEmpty()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new("Honda", "Accord", 2024, "")] }).Errors,
            e => e.PropertyName == "Vehicles[0].LicensePlate");
    }

    [Fact]
    public void Validate_ShouldFail_WhenLicensePlateExceeds10Characters()
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new("Honda", "Accord", 2024, new string('A', 11))] }).Errors,
            e => e.PropertyName == "Vehicles[0].LicensePlate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(9999)]
    public void Validate_ShouldFail_WhenVehicleYearIsOutOfRange(int year)
    {
        // Arrange & Act & Assert
        Assert.Contains(_validator.Validate(Valid() with { Vehicles = [new("Honda", "Accord", year, "ABC123")] }).Errors,
            e => e.PropertyName == "Vehicles[0].Year");
    }

    [Fact]
    public void Validate_ShouldReportTheInvalidVehicleIndex_WhenOnlySecondIsInvalid()
    {
        // Arrange
        var command = Valid() with
        {
            Vehicles = [new("Honda", "Accord", 2024, "ABC123"), new("", "Civic", 2024, "XYZ789")]
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == "Vehicles[1].Make");
        Assert.DoesNotContain(result.Errors, e => e.PropertyName.StartsWith("Vehicles[0]"));
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(Valid()).IsValid);
    }
}