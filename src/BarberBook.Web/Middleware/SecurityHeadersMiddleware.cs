namespace BarberBook.Web.Middleware;

/// <summary>
/// Security headers middleware enforcing HSTS, X-Content-Type-Options, X-Frame-Options,
/// Referrer-Policy, and Content-Security-Policy (P20).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers.Remove("Server");

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["X-XSS-Protection"] = "0";

            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                // Permits local inline Razor scripts, Google Fonts, and self origin assets
                headers.Append("Content-Security-Policy",
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline'; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                    "font-src 'self' https://fonts.gstatic.com data:; " +
                    "img-src 'self' data: https:; " +
                    "connect-src 'self'; " +
                    "frame-ancestors 'none';");
            }

            if (context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
            {
                headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

