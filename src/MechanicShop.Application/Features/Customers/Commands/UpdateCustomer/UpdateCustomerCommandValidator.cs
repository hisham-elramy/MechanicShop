using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public class UpdateCustomerCommandValidator :
AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .WithMessage("Customer ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email")
            .MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
          .NotEmpty().WithMessage("Phone number is required.")
          .Matches(@"^\+?\d{7,15}$").WithMessage("Phone number must be 7–15 digits and may start with '+'.");

        RuleFor(x => x.Vehicles)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vehicle list cannot be null.")
            .Must(p => p.Count > 0).WithMessage("At least one vehicle is required.");

        RuleForEach(x => x.Vehicles).SetValidator(new UpdateVehicleCommandValidator());
    }
}