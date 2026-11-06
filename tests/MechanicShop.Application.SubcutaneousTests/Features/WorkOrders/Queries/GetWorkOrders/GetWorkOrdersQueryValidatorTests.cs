using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrders;

public class GetWorkOrdersQueryValidatorTests
{
    private readonly GetWorkOrdersQueryValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenPageIsLessThanOne(int page)
    {
        // Arrange
        var command = new GetWorkOrdersQuery(page, 10, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetWorkOrdersQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_ShouldFail_WhenPageSizeIsOutOfRange(int pageSize)
    {
        // Arrange
        var command = new GetWorkOrdersQuery(1, pageSize, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetWorkOrdersQuery.PageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSortDirectionIsInvalid()
    {
        // Arrange
        var command = new GetWorkOrdersQuery(1, 10, null, SortDirection: "up");

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetWorkOrdersQuery.SortDirection));
    }

    [Fact]
    public void Validate_ShouldFail_WhenStartDateToIsBeforeStartDateFrom()
    {
        // Arrange
        var from = new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var command = new GetWorkOrdersQuery(1, 10, null, StartDateFrom: from, StartDateTo: from.AddDays(-1));

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
    {
        // Arrange
        var command = new GetWorkOrdersQuery(1, 10, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

}