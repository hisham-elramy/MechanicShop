using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrderById;

public class GetWorkOrderByIdQueryValidatorTests
{
    private readonly GetWorkOrderByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenWorkOrderIdIsEmpty()
    {
        var result = _validator.Validate(new GetWorkOrderByIdQuery(Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GetWorkOrderByIdQuery.WorkOrderId) &&
            e.ErrorCode == "WorkOrderId_Is_Required");
    }

    [Fact]
    public void Validate_ShouldPass_WhenWorkOrderIdIsProvided()
    {
        Assert.True(_validator.Validate(new GetWorkOrderByIdQuery(Guid.NewGuid())).IsValid);
    }
}