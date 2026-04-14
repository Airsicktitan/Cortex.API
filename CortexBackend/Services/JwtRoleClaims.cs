using System.Security.Claims;
using System.Text.Json;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Maps Auth0 role claims onto the principal. Primary claim: <see cref="CortexRolesClaimType"/> (array).
/// </summary>
public static class JwtRoleClaims
{
    /// <summary>Preferred multi-role claim from Auth0 Post-Login / RBAC.</summary>
    public const string CortexRolesClaimType = "https://cortex-api/roles";

    /// <summary>Legacy single-role claim.</summary>
    public const string CortexLegacySingleRoleClaimType = "https://cortex-api/role";

    /// <summary>
    /// Expands JWT role claims and adds one claim per role using <see cref="CortexRolesClaimType"/>
    /// and <see cref="ClaimTypes.Role"/> so <c>RequireRole</c> / <c>IsInRole</c> work.
    /// </summary>
    public static void AddNormalizedRoleClaims(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var resolved = ResolveRoles(principal);
        if (resolved.Count == 0)
        {
            return;
        }

        foreach (var role in resolved)
        {
            if (!identity.HasClaim(CortexRolesClaimType, role))
            {
                identity.AddClaim(new Claim(CortexRolesClaimType, role));
            }

            if (!identity.HasClaim(ClaimTypes.Role, role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }
    }

    /// <summary>Returns canonical Auth0 roles from the token (order preserved, duplicates removed).</summary>
    public static IReadOnlyList<string> ResolveRoles(ClaimsPrincipal principal)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void AddFromRaw(string? raw)
        {
            foreach (var token in ExpandRolesToStrings(raw))
            {
                if (Auth0Roles.TryNormalize(token, out var canonical) && seen.Add(canonical))
                {
                    list.Add(canonical);
                }
            }
        }

        AddFromRaw(principal.FindFirst(CortexRolesClaimType)?.Value);
        AddFromRaw(principal.FindFirst(CortexLegacySingleRoleClaimType)?.Value);
        AddFromRaw(principal.FindFirst("roles")?.Value);
        AddFromRaw(principal.FindFirst("role")?.Value);

        foreach (var claim in principal.FindAll(ClaimTypes.Role))
        {
            AddFromRaw(claim.Value);
        }

        return list;
    }

    private static IEnumerable<string> ExpandRolesToStrings(string? rawValue)
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
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Cast<string>()
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // fall through
            }
        }

        return [trimmed];
    }
}
