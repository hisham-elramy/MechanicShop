using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateVehicleCommandValidator :
AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Make)
            .NotEmpty().MaximumLength(50);

        RuleFor(x => x.Model)
            .NotEmpty().MaximumLength(50);

        RuleFor(x => x.LicensePlate)
            .NotEmpty().MaximumLength(10);

        RuleFor(x => x.Year)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 1)
            .WithMessage("Vehicle year is out of valid range.");
    }
}