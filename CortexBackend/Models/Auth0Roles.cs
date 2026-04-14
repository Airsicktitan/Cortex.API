namespace Cortex.API.Models;

/// <summary>
/// Canonical Auth0 role names (case-sensitive). Token claims are normalized to these values.
/// </summary>
public static class Auth0Roles
{
    public const string Admin = "Admin";
    public const string Developer = "Developer";
    public const string BusinessManager = "Business Manager";
    public const string User = "User";
    public const string Guest = "Guest";

    /// <summary>Ordered highest to lowest privilege (for DB snapshot / single-role fields).</summary>
    public static readonly string[] PrecedenceOrder =
    [
        Admin,
        Developer,
        BusinessManager,
        User,
        Guest
    ];

    public static bool TryNormalize(string? raw, out string canonical)
    {
        canonical = User;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        foreach (var role in PrecedenceOrder)
        {
            if (trimmed.Equals(role, StringComparison.OrdinalIgnoreCase))
            {
                canonical = role;
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the highest-privilege role present in <paramref name="roles"/>, or <see cref="User"/> if none match.</summary>
    public static string GetHighestRole(IEnumerable<string>? roles)
    {
        if (roles is null)
        {
            return User;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in roles)
        {
            if (TryNormalize(r, out var c))
            {
                set.Add(c);
            }
        }

        foreach (var role in PrecedenceOrder)
        {
            if (set.Contains(role))
            {
                return role;
            }
        }

        return User;
    }
}
