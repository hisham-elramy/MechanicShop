using FluentValidation;

namespace MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderState;

public class GetWorkOrderStatsQueryValidator
: AbstractValidator<GetWorkOrderStatsQuery>
{
    public GetWorkOrderStatsQueryValidator()
    {
        RuleFor(request => request.Date)
            .NotEmpty()
            .WithErrorCode("Date_Is_Required")
            .WithMessage("Date is required.");

        RuleFor(request => request.Date)
            .LessThan(DateOnly.MaxValue)
            .WithErrorCode("Date_Out_Of_Range")
            .WithMessage("Date is out of range.");
    }
}