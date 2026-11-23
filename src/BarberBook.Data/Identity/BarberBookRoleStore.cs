using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Data.Identity;

/// <summary>
/// Custom RoleStore that adapts ASP.NET Identity role and claim queries for document databases (MongoDB)
/// by evaluating ToClaim() in memory.
/// </summary>
public class BarberBookRoleStore : RoleStore<IdentityRole<int>, BarberBookDbContext, int>
{
    public BarberBookRoleStore(BarberBookDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
    }

    public override async Task<IList<Claim>> GetClaimsAsync(IdentityRole<int> role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(role);

        var roleClaims = await Context.RoleClaims
            .Where(rc => rc.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        return roleClaims.Select(c => c.ToClaim()).ToList();
    }
}

