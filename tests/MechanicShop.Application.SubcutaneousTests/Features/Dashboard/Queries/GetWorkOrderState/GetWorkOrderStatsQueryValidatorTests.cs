using MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderState;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Dashboard.Queries.GetWorkOrderState;

public class GetWorkOrderStatsQueryValidatorTests
{
    private readonly GetWorkOrderStatsQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenDateIsDefault()
    {
        // Arrange & Act
        var result = _validator.Validate(new GetWorkOrderStatsQuery(default));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GetWorkOrderStatsQuery.Date) &&
            e.ErrorCode == "Date_Is_Required");
    }


    [Fact]
    public void Validate_ShouldFail_WhenDateIsMaxValue()
    {
        // Arrange & Act
        var result = _validator.Validate(new GetWorkOrderStatsQuery(DateOnly.MaxValue));

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldPass_WhenDateIsValid()
    {
        // Arrange & Act & Assert
        Assert.True(_validator.Validate(new GetWorkOrderStatsQuery(new DateOnly(2030, 1, 1))).IsValid);
    }
}