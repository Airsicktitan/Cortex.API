using Microsoft.Extensions.Configuration;

namespace Cortex.API;

/// <summary>
/// Resolves the SQL connection string using the same key order as <see cref="Program"/>:
/// CortexDb (local Development), then AzureCortexDb / CortexDB for Azure and existing deployments.
/// </summary>
internal static class DatabaseConnectionConfiguration
{
    internal static readonly string[] ConnectionStringKeys = ["CortexDb", "AzureCortexDb", "CortexDB"];

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
