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
}

