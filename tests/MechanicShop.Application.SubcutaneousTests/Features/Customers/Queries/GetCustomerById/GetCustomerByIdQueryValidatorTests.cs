using MechanicShop.Application.Features.Customers.Queries.GetCustomerById;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomerById;

public class GetCustomerByIdQueryValidatorTests
{
    private readonly GetCustomerByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenCustomerIdIsEmpty()
    {
        // Arrange & Act
        var result = _validator.Validate(new GetCustomerByIdQuery(Guid.Empty));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GetCustomerByIdQuery.CustomerId) &&
            e.ErrorCode == "CustomerId_Is_Required");
    }

    [Fact]
    public void Validate_ShouldPass_WhenCustomerIdIsProvided()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new GetCustomerByIdQuery(Guid.NewGuid())).IsValid);
    }
}