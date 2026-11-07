using BarberBook.Web.Models.Admin;
using FluentValidation;

namespace BarberBook.Web.Validators;

public class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.")
            .MaximumLength(150).WithMessage("Branch name must not exceed 150 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Branch address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("Phone number must not exceed 30 characters.");

        RuleFor(x => x.OpeningTime)
            .NotEmpty().WithMessage("Opening time is required.");

        RuleFor(x => x.ClosingTime)
            .NotEmpty().WithMessage("Closing time is required.");
    }
}

public class UpdateBranchRequestValidator : AbstractValidator<UpdateBranchRequest>
{
    public UpdateBranchRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.")
            .MaximumLength(150).WithMessage("Branch name must not exceed 150 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Branch address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("Phone number must not exceed 30 characters.");

        RuleFor(x => x.OpeningTime)
            .NotEmpty().WithMessage("Opening time is required.");

        RuleFor(x => x.ClosingTime)
            .NotEmpty().WithMessage("Closing time is required.");
    }
}
