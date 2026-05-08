using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>Validates credential PUT bodies; never includes secret values in error paths.</summary>
public static class IntegrationCredentialSecretValidator
{
    public static void ValidateSecretsForConfigure(IntegrationProvider provider, Dictionary<string, string?>? secrets)
    {
        var profile = IntegrationProviderCatalog.Get(provider);
        var allowedSecretKeys = new HashSet<string>(
            profile.Fields.Where(f => f.IsSecret).Select(f => f.Key),
            StringComparer.OrdinalIgnoreCase);

        if (allowedSecretKeys.Count == 0)
        {
            throw new IntegrationApiException(400, "This provider does not use stored connection credentials.");
        }

        if (secrets is null || secrets.Count == 0)
        {
            throw new IntegrationApiException(400, "No credential fields were submitted.");
        }

        var nonEmptyCount = 0;
        foreach (var kv in secrets)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            var key = kv.Key.Trim();
            if (!allowedSecretKeys.Contains(key))
            {
                throw new IntegrationApiException(400, "Unsupported credential field for this provider.");
            }

            if (!string.IsNullOrWhiteSpace(kv.Value))
            {
                nonEmptyCount++;
            }
        }

        if (nonEmptyCount == 0)
        {
            throw new IntegrationApiException(400, "Provide at least one non-empty credential value.");
        }
    }
}
