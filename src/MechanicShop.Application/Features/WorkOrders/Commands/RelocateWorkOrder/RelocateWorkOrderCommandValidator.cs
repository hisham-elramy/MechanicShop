using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;

public sealed class RelocateWorkOrderCommandValidator
: AbstractValidator<RelocateWorkOrderCommand>
{
    public RelocateWorkOrderCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .WithMessage("WorkOrder ID is required.");
    
        RuleFor(x => x.NewSpot)
            .IsInEnum();

        RuleFor(x => x.NewStartAt)
            .Must(d => d > DateTimeOffset.UtcNow)
            .WithMessage("New start time must be in the future.");
    }
}