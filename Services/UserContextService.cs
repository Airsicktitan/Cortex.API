using System.Security.Claims;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class UserContextService(IUserRepository userRepository) : IUserContextService
{
    private readonly IUserRepository _userRepo = userRepository;

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

        var user = await _userRepo.GetByAuth0IdAsync(auth0Id);

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

            await _userRepo.CreateUserAsync(user);
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
            await _userRepo.SaveChangesAsync();

        return user;
    }

}