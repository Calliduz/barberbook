using System.Globalization;
using BarberBook.Web.Models.Schedule;
using FluentValidation;

namespace BarberBook.Web.Validators;

public class SetWorkingHoursRequestValidator : AbstractValidator<SetWorkingHoursRequest>
{
    public SetWorkingHoursRequestValidator()
    {
        RuleFor(x => x.Ranges)
            .NotNull().WithMessage("Working hours ranges cannot be null.");

        RuleForEach(x => x.Ranges).ChildRules(range =>
        {
            range.RuleFor(r => r.DayOfWeek)
                .InclusiveBetween(0, 6).WithMessage("Day of week must be between 0 (Sunday) and 6 (Saturday).");

            range.RuleFor(r => r.StartTime)
                .NotEmpty().WithMessage("Start time is required.")
                .Must(BeValidTime).WithMessage("Start time must be in HH:mm format (e.g., 09:00).");

            range.RuleFor(r => r.EndTime)
                .NotEmpty().WithMessage("End time is required.")
                .Must(BeValidTime).WithMessage("End time must be in HH:mm format (e.g., 18:00).");

            range.RuleFor(r => r)
                .Must(r =>
                {
                    if (!TryParseMinutes(r.StartTime, out var start) || !TryParseMinutes(r.EndTime, out var end))
                        return true;

                    // Support 00:00 as midnight closing
                    if (end == 0 && start > 0) end = 24 * 60;
                    return end > start;
                })
                .WithMessage("End time must be after start time.");
        });

        // Rule: No overlapping ranges within the same day
        RuleFor(x => x.Ranges)
            .Must(HaveNoOverlappingRanges)
            .WithMessage("Working hours cannot have overlapping ranges on the same day.");
    }

    private static bool BeValidTime(string time)
    {
        return TimeSpan.TryParseExact(time, "hh\\:mm", CultureInfo.InvariantCulture, out _) ||
               TimeSpan.TryParseExact(time, "h\\:mm", CultureInfo.InvariantCulture, out _);
    }

    private static bool TryParseMinutes(string time, out int minutes)
    {
        if (TimeSpan.TryParseExact(time, "hh\\:mm", CultureInfo.InvariantCulture, out var ts) ||
            TimeSpan.TryParseExact(time, "h\\:mm", CultureInfo.InvariantCulture, out ts))
        {
            minutes = (int)ts.TotalMinutes;
            return true;
        }

        minutes = 0;
        return false;
    }

    private static bool HaveNoOverlappingRanges(List<WorkingHoursRangeDto>? ranges)
    {
        if (ranges == null || ranges.Count <= 1) return true;

        var byDay = ranges.GroupBy(r => r.DayOfWeek);
        foreach (var group in byDay)
        {
            var parsedRanges = new List<(int Start, int End)>();
            foreach (var r in group)
            {
                if (!TryParseMinutes(r.StartTime, out var start) || !TryParseMinutes(r.EndTime, out var end))
                    continue;

                if (end == 0 && start > 0) end = 24 * 60;
                parsedRanges.Add((start, end));
            }

            parsedRanges.Sort((a, b) => a.Start.CompareTo(b.Start));
            for (int i = 0; i < parsedRanges.Count - 1; i++)
            {
                if (parsedRanges[i].End > parsedRanges[i + 1].Start)
                {
                    return false; // Overlap detected!
                }
            }
        }

        return true;
    }
}

public class CreateDayOffRequestValidator : AbstractValidator<CreateDayOffRequest>
{
    public CreateDayOffRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.")
            .Must(BeValidDate).WithMessage("Start date must be in YYYY-MM-DD format.");

        When(x => !string.IsNullOrWhiteSpace(x.EndDate), () =>
        {
            RuleFor(x => x.EndDate!)
                .Must(BeValidDate).WithMessage("End date must be in YYYY-MM-DD format.")
                .Must((req, endDate) =>
                {
                    if (DateTime.TryParse(req.StartDate, out var start) && DateTime.TryParse(endDate, out var end))
                    {
                        return end >= start;
                    }
                    return true;
                }).WithMessage("End date must be on or after start date.");
        });

        RuleFor(x => x.Reason)
            .MaximumLength(255).WithMessage("Reason must not exceed 255 characters.");
    }

    private static bool BeValidDate(string dateStr)
    {
        return DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}

