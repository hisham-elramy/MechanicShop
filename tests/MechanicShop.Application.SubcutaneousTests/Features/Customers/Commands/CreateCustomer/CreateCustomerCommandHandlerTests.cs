using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.CreateCustomer;


[Collection(WebAppFactoryCollection.CollectionName)]
public class CreateCustomerCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();


    [Fact]
    public async Task Handle_ShouldSucceedAndPersist_WhenCommandIsValid()
    {
        // Arrange
        var email = UniqueEmail();

        // Act
        var result = await _mediator.Send(Command(email));

        // Assert
        Assert.True(result.IsSuccess);

        var saved = await FindByEmailAsync(email);
        Assert.NotNull(saved);
        Assert.Equal("John Smith", saved.Name);
        Assert.Equal("+15555555555", saved.PhoneNumber);

        var vehicle = Assert.Single(saved.Vehicles);
        Assert.Equal("Honda", vehicle.Make);
        Assert.Equal("Accord", vehicle.Model);
        Assert.Equal(2024, vehicle.Year);
        Assert.Equal("ABC123", vehicle.LicensePlate);
    }

    [Fact]
    public async Task Handle_ShouldPersistAllVehicles_WhenSeveralAreGiven()
    {
        // Arrange
        var email = UniqueEmail();
        var command = Command(email) with
        {
            Vehicles =
            [
                new("Honda", "Accord", 2024, "AAA111"),
                new("Toyota", "Corolla", 2022, "BBB222"),
                new("Ford", "Focus", 2020, "CCC333"),
            ]
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, (await FindByEmailAsync(email))!.Vehicles.Count());
    }

    [Fact]
    public async Task Handle_ShouldTrimFields_WhenInputHasSurroundingSpaces()
    {
        // Arrange
        var email = UniqueEmail();

        // Act
        var result = await _mediator.Send
        (
            new CreateCustomerCommand
            (
                "  John Smith  ",
                "+15555555555",
                $"  {email}  ",
                [new("  Honda  ", "  Accord  ", 2024, "  ABC123  ")]
            )
        );

        // Assert
Assert.True(result.IsSuccess, result.IsError ? string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}")) : "ok");        var saved = await FindByEmailAsync(email);
        Assert.NotNull(saved);
        Assert.Equal("John Smith", saved.Name);
        Assert.Equal("Honda", saved.Vehicles.Single().Make);
    }

    [Fact]
    public async Task Handle_ShouldStoreEmailLowercased()
    {
        // Arrange
        var email = UniqueEmail();

        // Act
        await _mediator.Send(Command(email.ToUpperInvariant()));

        // Assert
        Assert.NotNull(await FindByEmailAsync(email));
    }

    [Fact]
    public async Task Handle_ShouldGenerateDistinctIds_ForTwoCustomers()
    {
        // Arrange & Act
        var first = await _mediator.Send(Command(UniqueEmail()));
        var second = await _mediator.Send(Command(UniqueEmail()));

        // Assert
        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.NotEqual(first.Value.CustomerId, second.Value.CustomerId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmailAlreadyExists()
    {
        // Arrange
        var email = UniqueEmail();

        // Act & Assert
        Assert.True((await _mediator.Send(Command(email))).IsSuccess);

        var result = await _mediator.Send(Command(email));
        Assert.True(result.IsError);
        Assert.Equal(CustomerErrors.CustomerExists.Code, result.TopError.Code);
        Assert.Equal(1, await CountByEmailAsync(email));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmailDiffersOnlyByCase()
    {
        // Arrange
        var email = UniqueEmail();

        // Act & Assert
        Assert.True((await _mediator.Send(Command(email))).IsSuccess);

        var result = await _mediator.Send(Command(email.ToUpperInvariant()));

        Assert.True(result.IsError);
        Assert.Equal(CustomerErrors.CustomerExists.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmailDiffersOnlyBySurroundingSpaces()
    {
        // Arrange
        var email = UniqueEmail();

        // Act & Assert
        Assert.True((await _mediator.Send(Command(email))).IsSuccess);

        var result = await _mediator.Send(Command($"  {email}  "));

        Assert.Equal(CustomerErrors.CustomerExists.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFailAndPersistNothing_WhenAVehicleIsInvalid()
    {
        // Arrange
        var email = UniqueEmail();
        var command = Command(email) with { Vehicles = [new("", "Accord", 2024, "ABC123")] };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsError);
        Assert.Null(await FindByEmailAsync(email));
    }

    [Fact]
    public async Task Handle_ShouldFailAndPersistNothing_WhenOneOfSeveralVehiclesIsInvalid()
    {
        // Arrange
        var email = UniqueEmail();
        var command = Command(email) with
        {
            Vehicles = [new("Honda", "Accord", 2024, "AAA111"), new("Toyota", "", 2022, "BBB222")]
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.IsError);
        Assert.Null(await FindByEmailAsync(email));
    }

    [Fact]
    public async Task Handle_ShouldFailAndPersistNothing_WhenNoVehicles()
    {
        // Arrange
        var email = UniqueEmail();

        // Act
        var result = await _mediator.Send(Command(email) with { Vehicles = [] });

        // Assert
        Assert.True(result.IsError);
        Assert.Null(await FindByEmailAsync(email));
    }


    private static string UniqueEmail()
    {
        return $"customer-{Guid.NewGuid():N}@example.com";
    }

    private static CreateCustomerCommand Command(string email)
    {
        return new
        (
            "John Smith",
            "+15555555555",
            email,
            [new CreateVehicleCommand("Honda", "Accord", 2024, "ABC123")]
        );
    }

    private Task<Customer?> FindByEmailAsync(string email)
    {
        return factory.CreateAppDbContext().Customers
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .SingleOrDefaultAsync(c => c.Email == email);
    }

    private Task<int> CountByEmailAsync(string email)
    {
        return factory.CreateAppDbContext().Customers.CountAsync(c => c.Email == email);
    }
}