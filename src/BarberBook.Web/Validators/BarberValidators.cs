using BarberBook.Web.Models.Admin;
using FluentValidation;

namespace BarberBook.Web.Validators;

public class CreateBarberRequestValidator : AbstractValidator<CreateBarberRequest>
{
    public CreateBarberRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .When(x => string.IsNullOrWhiteSpace(x.FullName))
            .MaximumLength(50).WithMessage("First name must not exceed 50 characters.");

        RuleFor(x => x.MiddleName)
            .MaximumLength(50).WithMessage("Middle name must not exceed 50 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .When(x => string.IsNullOrWhiteSpace(x.FullName))
            .MaximumLength(50).WithMessage("Last name must not exceed 50 characters.");

        RuleFor(x => x.FullName)
            .Must((req, _) => !string.IsNullOrWhiteSpace(req.ResolvedFullName))
            .WithMessage("First name and Last name are required.")
            .Must((req, _) => req.ResolvedFullName.Length <= 150)
            .WithMessage("Full name must not exceed 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Temporary password is required.")
            .MinimumLength(6).WithMessage("Temporary password must be at least 6 characters long.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(100).WithMessage("Display name must not exceed 100 characters.");

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Bio must not exceed 1000 characters.");

        RuleFor(x => x.PhotoUrl)
            .MaximumLength(500).WithMessage("Photo URL must not exceed 500 characters.");

        RuleFor(x => x.Specialties)
            .MaximumLength(255).WithMessage("Specialties must not exceed 255 characters.");

        RuleFor(x => x.CommissionRate)
            .InclusiveBetween(0m, 100m)
            .When(x => x.CommissionRate.HasValue)
            .WithMessage("Commission rate must be between 0% and 100%.");
    }
}

public class UpdateBarberRequestValidator : AbstractValidator<UpdateBarberRequest>
{
    public UpdateBarberRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .When(x => string.IsNullOrWhiteSpace(x.FullName))
            .MaximumLength(50).WithMessage("First name must not exceed 50 characters.");

        RuleFor(x => x.MiddleName)
            .MaximumLength(50).WithMessage("Middle name must not exceed 50 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .When(x => string.IsNullOrWhiteSpace(x.FullName))
            .MaximumLength(50).WithMessage("Last name must not exceed 50 characters.");

        RuleFor(x => x.FullName)
            .Must((req, _) => !string.IsNullOrWhiteSpace(req.ResolvedFullName))
            .WithMessage("First name and Last name are required.")
            .Must((req, _) => req.ResolvedFullName.Length <= 150)
            .WithMessage("Full name must not exceed 150 characters.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(100).WithMessage("Display name must not exceed 100 characters.");

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Bio must not exceed 1000 characters.");

        RuleFor(x => x.PhotoUrl)
            .MaximumLength(500).WithMessage("Photo URL must not exceed 500 characters.");

        RuleFor(x => x.Specialties)
            .MaximumLength(255).WithMessage("Specialties must not exceed 255 characters.");

        RuleFor(x => x.CommissionRate)
            .InclusiveBetween(0m, 100m)
            .When(x => x.CommissionRate.HasValue)
            .WithMessage("Commission rate must be between 0% and 100%.");
    }
}

public class ResetBarberPasswordRequestValidator : AbstractValidator<ResetBarberPasswordRequest>
{
    public ResetBarberPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");
    }
}

