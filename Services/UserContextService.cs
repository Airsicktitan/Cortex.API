using System.Security.Claims;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class UserContextService : IUserContextService
{
    private readonly CortexDbContext _dbContext;

    public UserContextService(CortexDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var auth0Id = principal.FindFirst("sub")?.Value;
        var changed = false;

        if (string.IsNullOrEmpty(auth0Id))
            throw new UnauthorizedAccessException("Missing Sub Claim.");

        var email = principal.FindFirst("email")?.Value ??
                    principal.FindFirst(ClaimTypes.Email)?.Value ??
                    "unknown@email.com";

        var displayName = principal.FindFirst("name")?.Value ??
                        principal.FindFirst(ClaimTypes.Name)?.Value ??
                        email;

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);

        if (user == null)
        {
            user = new User
            {
                Auth0Id = auth0Id,
                DisplayName = displayName,
                Email = email,
                CreatedDate = DateTime.UtcNow,
                LastLoginDate = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            changed = true;
        }

        // Update display name if it changed in Auth0
        if (user.DisplayName != displayName && !string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
            changed = true;
        }

        if (user.LastLoginDate == null || user.LastLoginDate < DateTime.UtcNow.AddMinutes(-10))
        {
            user.LastLoginDate = DateTime.UtcNow;
            changed = true;
        }

        if (changed)
            await _dbContext.SaveChangesAsync();

        return user;
    }

}