using BarberBook.Web.Common;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace BarberBook.Tests;

public class FormattingHelpersTests
{
    [Fact]
    public void FormatPeso_WithSampleAmount_FormatsCorrectlyWithPesoSymbolAndDecimals()
    {
        // Arrange
        decimal amount = 1340.00m;

        // Act
        var result = FormattingHelpers.FormatPeso(amount);

        // Assert
        result.Should().Be("₱1,340.00");
    }

    [Theory]
    [InlineData(150.00, "₱150.00")]
    [InlineData(220.50, "₱220.50")]
    [InlineData(0.00, "₱0.00")]
    [InlineData(500.00, "₱500.00")]
    [InlineData(1000000.00, "₱1,000,000.00")]
    public void FormatPeso_WithVariousAmounts_FormatsExpectedStrings(decimal amount, string expected)
    {
        // Act
        var result = FormattingHelpers.FormatPeso(amount);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatTime12h_WithUtcInstant_FormatsInAsiaManilaTime()
    {
        // Arrange: 2026-09-14 01:00:00 UTC corresponds to 9:00 AM in Asia/Manila (+08:00)
        var utcInstant = Instant.FromUtc(2026, 9, 14, 1, 0, 0);

        // Act
        var result = FormattingHelpers.FormatTime12h(utcInstant);

        // Assert
        result.Should().Be("9:00 AM");
    }

    [Fact]
    public void FormatTime12h_WithAfternoonTime_FormatsAsPm()
    {
        // Arrange: 2026-09-14 05:00:00 UTC corresponds to 1:00 PM in Asia/Manila (+08:00)
        var utcInstant = Instant.FromUtc(2026, 9, 14, 5, 0, 0);

        // Act
        var result = FormattingHelpers.FormatTime12h(utcInstant);

        // Assert
        result.Should().Be("1:00 PM");
    }

    [Fact]
    public void FormatTime12h_WithDateTimeOffset_FormatsCorrectly()
    {
        // Arrange: UTC DateTimeOffset
        var dto = new DateTimeOffset(2026, 9, 14, 2, 30, 0, TimeSpan.Zero);

        // Act (UTC 2:30 AM + 8 hrs = 10:30 AM in Manila)
        var result = FormattingHelpers.FormatTime12h(dto);

        // Assert
        result.Should().Be("10:30 AM");
    }

    [Fact]
    public void FormatDate_WithUtcInstant_FormatsToMonthDayYear()
    {
        // Arrange: 2026-09-14 01:00:00 UTC is Sep 14, 2026 in Manila
        var utcInstant = Instant.FromUtc(2026, 9, 14, 1, 0, 0);

        // Act
        var result = FormattingHelpers.FormatDate(utcInstant);

        // Assert
        result.Should().Be("Sep 14, 2026");
    }
}

