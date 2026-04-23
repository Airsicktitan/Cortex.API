using Microsoft.Extensions.Configuration;

namespace Cortex.API;

/// <summary>
/// Resolves the SQL connection string using the same key order as <see cref="Program"/>:
/// Uses ConnectionStrings:CortexDb across all environments.
/// </summary>
internal static class DatabaseConnectionConfiguration
{
    internal static readonly string[] ConnectionStringKeys = ["CortexDb"];

    internal static string? ResolveFirstNonEmpty(IConfiguration configuration)
    {
        foreach (var key in ConnectionStringKeys)
        {
            var candidate = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
