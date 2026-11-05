using BarberBook.Web.Models.Appointment;
using FluentValidation;

namespace BarberBook.Web.Validators;

public class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.ServiceId)
            .GreaterThan(0)
            .WithMessage("A valid service must be selected.");

        RuleFor(x => x.BarberId)
            .GreaterThan(0)
            .WithMessage("A valid barber must be selected.");

        RuleFor(x => x.StartsAt)
            .NotEmpty()
            .WithMessage("A valid startsAt timestamp is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}

public class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.StartsAt)
            .NotEmpty()
            .WithMessage("A valid new startsAt timestamp is required.");
    }
}
