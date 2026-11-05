using BarberBook.Data.Entities;
using BarberBook.Web.Common;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class ScheduleFormattingTests
{
    [Fact]
    public void FormatWeeklySchedule_StandardMonToSat_ReturnsFormattedRange()
    {
        // Monday (1) through Saturday (6) from 09:00 to 18:00
        var hours = new List<WorkingHours>();
        for (int dow = 1; dow <= 6; dow++)
        {
            hours.Add(new WorkingHours
            {
                DayOfWeek = dow,
                StartTime = new LocalTime(9, 0),
                EndTime = new LocalTime(18, 0)
            });
        }

        var result = FormattingHelpers.FormatWeeklySchedule(hours);

        result.Should().Be("Mon–Sat 9:00 AM – 6:00 PM");
    }

    [Fact]
    public void FormatWeeklySchedule_WeekdayAndWeekendDifferent_ReturnsGroupedRanges()
    {
        // Monday to Friday: 09:00 - 18:00
        // Saturday: 09:00 - 15:00
        var hours = new List<WorkingHours>();
        for (int dow = 1; dow <= 5; dow++)
        {
            hours.Add(new WorkingHours
            {
                DayOfWeek = dow,
                StartTime = new LocalTime(9, 0),
                EndTime = new LocalTime(18, 0)
            });
        }
        hours.Add(new WorkingHours
        {
            DayOfWeek = 6,
            StartTime = new LocalTime(9, 0),
            EndTime = new LocalTime(15, 0)
        });

        var result = FormattingHelpers.FormatWeeklySchedule(hours);

        result.Should().Be("Mon–Fri 9:00 AM – 6:00 PM, Sat 9:00 AM – 3:00 PM");
    }

    [Fact]
    public void FormatWeeklySchedule_SplitShifts_ReturnsFormattedShifts()
    {
        // Mon–Sat with 2 shifts per day: 09:00 - 12:00, 13:00 - 18:00
        var hours = new List<WorkingHours>();
        for (int dow = 1; dow <= 6; dow++)
        {
            hours.Add(new WorkingHours
            {
                DayOfWeek = dow,
                StartTime = new LocalTime(9, 0),
                EndTime = new LocalTime(12, 0)
            });
            hours.Add(new WorkingHours
            {
                DayOfWeek = dow,
                StartTime = new LocalTime(13, 0),
                EndTime = new LocalTime(18, 0)
            });
        }

        var result = FormattingHelpers.FormatWeeklySchedule(hours);

        result.Should().Be("Mon–Sat 9:00 AM – 12:00 PM, 1:00 PM – 6:00 PM");
    }

    [Fact]
    public void FormatWeeklySchedule_AllSevenDays_ReturnsMonToSun()
    {
        // 0 (Sun) to 6 (Sat) all 10:00 - 20:00
        var hours = new List<WorkingHours>();
        for (int dow = 0; dow <= 6; dow++)
        {
            hours.Add(new WorkingHours
            {
                DayOfWeek = dow,
                StartTime = new LocalTime(10, 0),
                EndTime = new LocalTime(20, 0)
            });
        }

        var result = FormattingHelpers.FormatWeeklySchedule(hours);

        result.Should().Be("Mon–Sun 10:00 AM – 8:00 PM");
    }

    [Fact]
    public void FormatWeeklySchedule_SingleDay_ReturnsSingleDayName()
    {
        var hours = new List<WorkingHours>
        {
            new()
            {
                DayOfWeek = 1, // Mon
                StartTime = new LocalTime(9, 0),
                EndTime = new LocalTime(18, 0)
            }
        };

        var result = FormattingHelpers.FormatWeeklySchedule(hours);

        result.Should().Be("Mon 9:00 AM – 6:00 PM");
    }

    [Fact]
    public void FormatWeeklySchedule_EmptyOrNull_ReturnsNoWorkingHoursSet()
    {
        FormattingHelpers.FormatWeeklySchedule(null).Should().Be("No working hours set");
        FormattingHelpers.FormatWeeklySchedule(new List<WorkingHours>()).Should().Be("No working hours set");
    }
}

