using System.Security.Claims;
using BarberBook.Data.Enums;

namespace BarberBook.Web.Common;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : null;
    }

    public static int GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var id = principal.GetUserId();
        if (!id.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated or has an invalid user ID claim.");
        }
        return id.Value;
    }

    public static string? GetUserRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role);
    }

    public static bool IsInUserRole(this ClaimsPrincipal principal, UserRole role)
    {
        return principal.IsInRole(role.ToString());
    }
}

