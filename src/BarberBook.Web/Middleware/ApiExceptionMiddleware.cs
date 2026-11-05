using System.Net;
using System.Text.Json;
using BarberBook.Web.Common;

namespace BarberBook.Web.Middleware;

public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            // Handle 401 and 403 for API/JSON requests
            if ((context.Response.StatusCode == (int)HttpStatusCode.Unauthorized ||
                 context.Response.StatusCode == (int)HttpStatusCode.Forbidden) &&
                IsApiRequest(context) && !context.Response.HasStarted)
            {
                var code = context.Response.StatusCode == (int)HttpStatusCode.Unauthorized
                    ? "UNAUTHORIZED"
                    : "FORBIDDEN";
                var message = context.Response.StatusCode == (int)HttpStatusCode.Unauthorized
                    ? "Authentication is required to access this resource."
                    : "You do not have permission to access this resource.";

                context.Response.ContentType = "application/json";
                var response = new ApiErrorResponse(code, message);
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing request {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("The response has already started, exception middleware cannot write error response.");
                throw;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var code = "INTERNAL_SERVER_ERROR";
        var message = "An unexpected error occurred.";

        var response = new ApiErrorResponse(code, message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static bool IsApiRequest(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var accept = context.Request.Headers["Accept"].ToString();
        return path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
               accept.Contains("application/json");
    }
}

