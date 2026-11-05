using System.Text.Json.Serialization;

namespace BarberBook.Web.Common;

public class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public ApiErrorBody Error { get; set; } = new();

    public ApiErrorResponse() { }

    public ApiErrorResponse(string code, string message, object? details = null)
    {
        Error = new ApiErrorBody
        {
            Code = code,
            Message = message,
            Details = details
        };
    }
}

public class ApiErrorBody
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; set; }
}

