using System.Globalization;
using NodaTime;

namespace BarberBook.Web.Common;

public static class FormattingHelpers
{
    private static readonly DateTimeZone ManilaZone = DateTimeZoneProviders.Tzdb["Asia/Manila"];
    private static readonly CultureInfo EnUsCulture = new("en-US");

    /// <summary>
    /// Formats decimal amount into Philippine Peso representation, e.g. 1340.00 -> "₱1,340.00".
    /// </summary>
    public static string FormatPeso(decimal amount)
    {
        return $"₱{amount.ToString("N2", EnUsCulture)}";
    }

    /// <summary>
    /// Formats a NodaTime Instant into 12-hour time in Asia/Manila timezone, e.g. "9:00 AM".
    /// </summary>
    public static string FormatTime12h(Instant utcInstant)
    {
        var zonedDateTime = utcInstant.InZone(ManilaZone);
        return zonedDateTime.ToString("h:mm tt", EnUsCulture);
    }

    /// <summary>
    /// Formats a DateTimeOffset into 12-hour time in Asia/Manila timezone, e.g. "9:00 AM".
    /// </summary>
    public static string FormatTime12h(DateTimeOffset dateTimeOffset)
    {
        var instant = Instant.FromDateTimeOffset(dateTimeOffset);
        return FormatTime12h(instant);
    }

    /// <summary>
    /// Formats a UTC DateTime into 12-hour time in Asia/Manila timezone, e.g. "9:00 AM".
    /// </summary>
    public static string FormatTime12h(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        var instant = Instant.FromDateTimeUtc(utc);
        return FormatTime12h(instant);
    }

    /// <summary>
    /// Formats a NodaTime Instant into standard date format in Asia/Manila timezone, e.g. "Sep 14, 2026".
    /// </summary>
    public static string FormatDate(Instant utcInstant)
    {
        var zonedDateTime = utcInstant.InZone(ManilaZone);
        return zonedDateTime.ToString("MMM d, yyyy", EnUsCulture);
    }

    /// <summary>
    /// Formats a NodaTime Instant into readable date and 12-hour time in Asia/Manila, e.g. "Sep 14, 2026, 9:00 AM".
    /// </summary>
    public static string FormatDateTime(Instant utcInstant)
    {
        var zonedDateTime = utcInstant.InZone(ManilaZone);
        return zonedDateTime.ToString("MMM d, yyyy, h:mm tt", EnUsCulture);
    }

    /// <summary>
    /// Formats a NodaTime LocalTime into 12-hour time, e.g. "9:00 AM".
    /// </summary>
    public static string FormatTime12h(LocalTime time)
    {
        return time.ToString("h:mm tt", EnUsCulture);
    }

    /// <summary>
    /// Formats a weekly schedule into human-readable text, e.g. "Mon–Sat 9:00 AM – 6:00 PM".
    /// </summary>
    public static string FormatWeeklySchedule(IEnumerable<BarberBook.Data.Entities.WorkingHours>? workingHours)
    {
        if (workingHours == null) return "No working hours set";

        // Day of week order: Mon (1) to Sun (0)
        var dayOrder = new[] { 1, 2, 3, 4, 5, 6, 0 };
        var dayNames = new Dictionary<int, string>
        {
            { 1, "Mon" }, { 2, "Tue" }, { 3, "Wed" },
            { 4, "Thu" }, { 5, "Fri" }, { 6, "Sat" }, { 0, "Sun" }
        };

        var hoursByDay = workingHours
            .GroupBy(w => w.DayOfWeek)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.OrderBy(w => w.StartTime)
                    .Select(w => $"{FormatTime12h(w.StartTime)} – {FormatTime12h(w.EndTime)}"))
            );

        var groups = new List<(List<int> Days, string Hours)>();
        (List<int> Days, string Hours)? currentGroup = null;

        foreach (var dow in dayOrder)
        {
            if (hoursByDay.TryGetValue(dow, out var hoursStr) && !string.IsNullOrWhiteSpace(hoursStr))
            {
                if (currentGroup != null && currentGroup.Value.Hours == hoursStr)
                {
                    currentGroup.Value.Days.Add(dow);
                }
                else
                {
                    if (currentGroup != null)
                    {
                        groups.Add(currentGroup.Value);
                    }
                    currentGroup = (new List<int> { dow }, hoursStr);
                }
            }
            else
            {
                if (currentGroup != null)
                {
                    groups.Add(currentGroup.Value);
                    currentGroup = null;
                }
            }
        }

        if (currentGroup != null)
        {
            groups.Add(currentGroup.Value);
        }

        if (!groups.Any())
        {
            return "No working hours set";
        }

        var formattedGroups = groups.Select(g =>
        {
            if (g.Days.Count == 1)
            {
                return $"{dayNames[g.Days[0]]} {g.Hours}";
            }
            return $"{dayNames[g.Days[0]]}–{dayNames[g.Days[^1]]} {g.Hours}";
        });

        return string.Join(", ", formattedGroups);
    }
}

