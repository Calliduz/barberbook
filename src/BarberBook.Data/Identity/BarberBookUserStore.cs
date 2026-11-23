using System.Security.Claims;
using BarberBook.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Data.Identity;

/// <summary>
/// Custom UserStore that adapts ASP.NET Identity role, claim, and user queries for document databases (MongoDB)
/// by avoiding relational cross-collection LINQ joins and untranslatable expressions like i.ToClaim().
/// </summary>
public class BarberBookUserStore : UserStore<ApplicationUser, IdentityRole<int>, BarberBookDbContext, int>
{
    public BarberBookUserStore(BarberBookDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
    }

    public override async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);

        var roles = new List<string>();
        if (user.Role != 0)
        {
            roles.Add(user.Role.ToString());
        }

        var roleIds = await Context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count > 0)
        {
            var dbRoles = await Context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync(cancellationToken);

            foreach (var r in dbRoles)
            {
                if (!roles.Contains(r, StringComparer.OrdinalIgnoreCase))
                {
                    roles.Add(r);
                }
            }
        }

        return roles;
    }

    public override async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken = default)
    {
        var roles = await GetRolesAsync(user, cancellationToken);
        return roles.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
    }

    public override async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(roleName);

        var role = await Context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == roleName.ToUpperInvariant(), cancellationToken);
        if (role == null) return new List<ApplicationUser>();

        var userIds = await Context.UserRoles
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        return await Context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public override async Task<IList<Claim>> GetClaimsAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);

        // Fetch claims first to evaluate ToClaim() in memory since MongoDB LINQ provider cannot translate ToClaim()
        var userClaims = await Context.UserClaims
            .Where(uc => uc.UserId == user.Id)
            .ToListAsync(cancellationToken);

        return userClaims.Select(c => c.ToClaim()).ToList();
    }

    public override async Task<IList<ApplicationUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(claim);

        var userIds = await Context.UserClaims
            .Where(uc => uc.ClaimValue == claim.Value && uc.ClaimType == claim.Type)
            .Select(uc => uc.UserId)
            .ToListAsync(cancellationToken);

        return await Context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public override async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);

        var logins = await Context.UserLogins
            .Where(l => l.UserId == user.Id)
            .ToListAsync(cancellationToken);

        return logins.Select(l => new UserLoginInfo(l.LoginProvider, l.ProviderKey, l.ProviderDisplayName)).ToList();
    }

    public override async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var userLogin = await Context.UserLogins
            .FirstOrDefaultAsync(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey, cancellationToken);

        if (userLogin == null) return null;

        return await Context.Users.FirstOrDefaultAsync(u => u.Id == userLogin.UserId, cancellationToken);
    }
}
