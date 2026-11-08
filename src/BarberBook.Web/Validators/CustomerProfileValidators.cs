using BarberBook.Web.Models.Customer;
using FluentValidation;

namespace BarberBook.Web.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(75).WithMessage("First name must not exceed 75 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(75).WithMessage("Last name must not exceed 75 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(25).WithMessage("Phone number must not exceed 25 characters.");
    }
}

