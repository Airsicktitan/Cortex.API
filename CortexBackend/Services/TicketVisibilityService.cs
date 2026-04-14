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

        var jwtRoles = JwtRoleClaims.ResolveRoles(principal);
        var scope = ResolveScope(jwtRoles);

        return new TicketVisibilityContext(user.Id, user.DisplayName, user.Email, scope);
    }

    private static TicketVisibilityScope ResolveScope(IReadOnlyList<string> jwtRoles)
    {
        var set = new HashSet<string>(jwtRoles, StringComparer.OrdinalIgnoreCase);

        if (set.Contains(Auth0Roles.Admin) ||
            set.Contains(Auth0Roles.Developer) ||
            set.Contains(Auth0Roles.BusinessManager))
        {
            return TicketVisibilityScope.All;
        }

        if (set.Contains(Auth0Roles.Guest))
        {
            return TicketVisibilityScope.AssignedToCurrentUser;
        }

        return TicketVisibilityScope.CreatedByCurrentUser;
    }
}
