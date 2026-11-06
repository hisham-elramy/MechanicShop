using MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.RemoveCustomer;

public class RemoveCustomerCommandValidatorTests
{
    private readonly RemoveCustomerCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenCustomerIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new RemoveCustomerCommand(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RemoveCustomerCommand.CustomerId));
    }

    [Fact]
    public void Validate_ShouldPass_WhenCustomerIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new RemoveCustomerCommand(Guid.NewGuid())).IsValid);
    }

}