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
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        if (IsApiRequest(context))
        {
            context.Response.ContentType = "application/json";
            var code = "INTERNAL_SERVER_ERROR";
            var message = "An unexpected error occurred.";

            var response = new ApiErrorResponse(code, message);
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
        else
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            var html = @"<!DOCTYPE html>
<html lang=""en"" class=""h-full bg-slate-950 text-slate-100 font-sans"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Service Unavailable - BarberBook</title>
    <link rel=""stylesheet"" href=""/css/site.css"" />
</head>
<body class=""flex flex-col items-center justify-center min-h-screen px-4 text-center bg-slate-950 text-slate-100 selection:bg-amber-500 selection:text-slate-950"">
    <div class=""max-w-md w-full p-8 rounded-3xl bg-slate-900/90 border border-slate-800 shadow-2xl backdrop-blur-md"">
        <div class=""barber-pole-icon mx-auto mb-4"" style=""height: 36px; width: 18px;""></div>
        <h1 class=""text-2xl font-serif font-black text-amber-400 mb-2"">Temporarily Unavailable</h1>
        <p class=""text-slate-300 text-sm mb-6 leading-relaxed"">The grooming workstation encountered an unexpected error. Please refresh the page or return to the main dashboard.</p>
        <div class=""flex items-center justify-center gap-3"">
            <a href=""/"" class=""px-5 py-2.5 rounded-xl text-xs font-bold bg-amber-500 text-slate-950 hover:bg-amber-400 transition shadow-lg"">Return Home</a>
            <button onclick=""window.location.reload()"" class=""px-5 py-2.5 rounded-xl text-xs font-bold bg-slate-800 text-slate-200 border border-slate-700 hover:bg-slate-700 transition"">Reload Page</button>
        </div>
    </div>
</body>
</html>";
            await context.Response.WriteAsync(html);
        }
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

