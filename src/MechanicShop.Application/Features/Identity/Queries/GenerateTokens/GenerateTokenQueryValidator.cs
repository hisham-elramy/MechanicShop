using FluentValidation;

namespace MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

public sealed class GenerateTokenQueryValidator
: AbstractValidator<GenerateTokenQuery>
{
    public GenerateTokenQueryValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Email_Null_Or_Empty").WithMessage("Email cannot be null or empty")
            .EmailAddress().WithErrorCode("Email_Invalid").WithMessage("Email is not valid.")
            .MaximumLength(256).WithErrorCode("Email_Too_Long");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Password_Null_Or_Empty").WithMessage("Password cannot be null or empty.")
            .MaximumLength(128).WithErrorCode("Password_Too_Long");
    }
}