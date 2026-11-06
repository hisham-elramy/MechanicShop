using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;

public class GetWorkOrdersQueryValidator : AbstractValidator<GetWorkOrdersQuery>
{
    public GetWorkOrdersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.SortDirection)
            .Must(d => d.Equals("asc", StringComparison.OrdinalIgnoreCase)
                    || d.Equals("desc", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.StartDateTo)
            .GreaterThanOrEqualTo(x => x.StartDateFrom!.Value)
            .When(x => x.StartDateFrom.HasValue && x.StartDateTo.HasValue);
    }
}