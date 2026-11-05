using System.Text.Json;
using BarberBook.Web.Common;
using FluentAssertions;
using Xunit;

namespace BarberBook.Tests;

public class ApiErrorResponseTests
{
    [Fact]
    public void ApiErrorResponse_SerializesWithExactExpectedErrorShape()
    {
        // Arrange
        var response = new ApiErrorResponse("VALIDATION_ERROR", "Invalid input parameters", new { field = "name" });

        // Act
        var json = JsonSerializer.Serialize(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert: { error: { code, message, details? } }
        root.TryGetProperty("error", out var errorProp).Should().BeTrue();
        errorProp.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        errorProp.GetProperty("message").GetString().Should().Be("Invalid input parameters");
        errorProp.TryGetProperty("details", out var detailsProp).Should().BeTrue();
        detailsProp.GetProperty("field").GetString().Should().Be("name");
    }

    [Fact]
    public void ApiErrorResponse_WithoutDetails_OmitsDetailsProperty()
    {
        // Arrange
        var response = new ApiErrorResponse("NOT_FOUND", "Resource not found");

        // Act
        var json = JsonSerializer.Serialize(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        root.GetProperty("error").TryGetProperty("details", out _).Should().BeFalse();
    }
}

