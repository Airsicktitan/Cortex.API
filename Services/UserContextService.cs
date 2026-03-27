using System.Security.Claims;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class UserContextService(IUserRepository userRepository, IHttpContextAccessor httpContextAccessor) : IUserContextService
{
    private readonly IUserRepository _userRepo = userRepository;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<User> GetCurrentUserAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var changed = false;

        if (principal is null || principal.Identity is null || !principal.Identity.IsAuthenticated)
            throw new UnauthorizedAccessException("No authenticated user found.");

        var auth0Id = principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(auth0Id))
            throw new UnauthorizedAccessException("Missing Sub Claim.");

        var email = principal.FindFirst("email")?.Value ??
                    principal.FindFirst(ClaimTypes.Email)?.Value ??
                    "unknown@email.com";

        var displayName = principal.FindFirst("https://cortex-api/display_name")?.Value ??
                        principal.FindFirst("name")?.Value ??
                        principal.FindFirst("nickname")?.Value ??
                        principal.FindFirst("preferred_username")?.Value ??
                        principal.FindFirst("username")?.Value ??
                        principal.FindFirst(ClaimTypes.Name)?.Value ??
                        email.Split('@')[0]; // Fallback to email prefix if no name claim is found 

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

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var shouldUpdateDisplayName =
                string.IsNullOrWhiteSpace(user.DisplayName) ||
                user.DisplayName != displayName ||
                LooksLikeEmail(user.DisplayName);

                if (shouldUpdateDisplayName)
                {
                    user.DisplayName = displayName;
                    changed = true;
                }
        }

        if (changed)
            await _userRepo.SaveChangesAsync();

        foreach (var claim in principal.Claims)
        {
            Console.WriteLine($"{claim.Type}: {claim.Value}");
        }   

        return user;
    }

    public async Task<User> UpdateProfileAsync(User user, UpdateUserProfileRequest request)
    {
        // Update only allowed fields
        user.DisplayName = request.DisplayName ?? user.DisplayName;
        user.Department = request.Department ?? user.Department;

        await _userRepo.SaveChangesAsync();

        return user;
    }

    public bool LooksLikeEmail(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Contains("@");
    }
}