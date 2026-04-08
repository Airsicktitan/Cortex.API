using System.Security.Claims;
using System.Text.Json;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class TicketVisibilityService(
    IUserContextService userContext,
    IHttpContextAccessor httpContextAccessor) : ITicketVisibilityService
{
    private readonly IUserContextService _userContext = userContext;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<TicketVisibilityContext> GetCurrentVisibilityAsync()
    {
        var user = await _userContext.GetCurrentUserAsync();
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal is null || principal.Identity is null || !principal.Identity.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("No authenticated user found.");
        }

        var roles = GetClaimValues(principal, "https://cortex-api/roles", "roles", "role", ClaimTypes.Role);
        var permissions = GetClaimValues(principal, "permissions");
        var scope = ResolveScope(user, roles, permissions);

        return new TicketVisibilityContext(user.Id, user.DisplayName, user.Email, scope);
    }

    private static TicketVisibilityScope ResolveScope(
        User user,
        ISet<string> roles,
        ISet<string> permissions)
    {
        if (permissions.Contains("admin:system") ||
            roles.Contains("admin") ||
            user.Role == UserRole.Admin)
        {
            return TicketVisibilityScope.All;
        }

        // The current DB enum still uses Manager, so treat that as the closest
        // equivalent to the newer BusinessUser concept until the role model is unified.
        if (roles.Contains("businessuser") ||
            roles.Contains("business user") ||
            permissions.Contains("business:user") ||
            user.Role == UserRole.Manager)
        {
            return TicketVisibilityScope.All;
        }

        if (roles.Contains("developer") || permissions.Contains("developer"))
        {
            return TicketVisibilityScope.AssignedToCurrentUser;
        }

        return TicketVisibilityScope.CreatedByCurrentUser;
    }

    private static HashSet<string> GetClaimValues(ClaimsPrincipal principal, params string[] claimTypes)
    {
        return principal.Claims
            .Where(claim => claimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
            .SelectMany(claim => ExpandClaimValue(claim.Value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ExpandClaimValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        var trimmed = rawValue.Trim();

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);

                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return document.RootElement
                        .EnumerateArray()
                        .Where(element => element.ValueKind == JsonValueKind.String)
                        .Select(element => element.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Cast<string>()
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // Fall back to treating the claim as a single plain-text value.
            }
        }

        return [trimmed];
    }
}
