using System.Globalization;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Resolves ticket owner fields (Syniti/Business owner) stored as free-form strings.
/// Supports legacy display names, emails, and canonical <c>user:{id}</c> tokens from the UI.
/// </summary>
public static class OwnerFieldResolution
{
    public const string UserIdTokenPrefix = "user:";

    public static Dictionary<string, User> BuildAliasLookup(IEnumerable<User> users)
    {
        var aliases = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in users)
        {
            AddAlias(aliases, "email", user.Email, user);
            AddAlias(aliases, "display", user.DisplayName, user);
            AddAlias(aliases, "nickname", user.NickName, user);
            AddAlias(aliases, "userid", user.Id.ToString(CultureInfo.InvariantCulture), user);
        }

        return aliases;
    }

    public static User? ResolveUser(string? rawOwner, IReadOnlyDictionary<string, User> aliases)
    {
        var normalized = Normalize(rawOwner);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        // Canonical id token from UI: user:123
        if (normalized.StartsWith(UserIdTokenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var idPart = normalized[UserIdTokenPrefix.Length..];
            if (aliases.TryGetValue($"userid:{idPart}", out var byUserId))
            {
                return byUserId;
            }
        }

        if (normalized.Contains('@', StringComparison.Ordinal) &&
            aliases.TryGetValue($"email:{normalized}", out var byEmail))
        {
            return byEmail;
        }

        if (aliases.TryGetValue($"display:{normalized}", out var byDisplayName))
        {
            return byDisplayName;
        }

        if (aliases.TryGetValue($"nickname:{normalized}", out var byNickname))
        {
            return byNickname;
        }

        if (aliases.TryGetValue($"email:{normalized}", out byEmail))
        {
            return byEmail;
        }

        return null;
    }

    public static bool TokenMatchesUser(
        User user,
        string? rawOwner,
        IReadOnlyDictionary<string, User> aliases)
    {
        var resolved = ResolveUser(rawOwner, aliases);
        return resolved?.Id == user.Id;
    }

    /// <summary>
    /// Human-readable owner label for API responses. Does not emit raw <c>user:</c> tokens when unresolved.
    /// </summary>
    public static string? FormatOwnerDisplayForApi(string? rawStored, User? resolved)
    {
        if (string.IsNullOrWhiteSpace(rawStored))
        {
            return null;
        }

        if (resolved != null)
        {
            if (!string.IsNullOrWhiteSpace(resolved.DisplayName))
            {
                return resolved.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(resolved.Email))
            {
                return resolved.Email.Trim();
            }

            return "Unknown";
        }

        var trimmed = rawStored.Trim();
        var norm = Normalize(rawStored);
        if (norm.StartsWith(UserIdTokenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown owner";
        }

        if (norm.Contains('@', StringComparison.Ordinal))
        {
            return trimmed;
        }

        return trimmed;
    }

    private static void AddAlias(
        IDictionary<string, User> aliases,
        string prefix,
        string? rawValue,
        User user)
    {
        var normalized = Normalize(rawValue);
        if (string.IsNullOrEmpty(normalized) || aliases.ContainsKey($"{prefix}:{normalized}"))
        {
            return;
        }

        aliases[$"{prefix}:{normalized}"] = user;
    }

    private static string Normalize(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? string.Empty
            : rawValue.Trim().ToLowerInvariant();
    }
}
