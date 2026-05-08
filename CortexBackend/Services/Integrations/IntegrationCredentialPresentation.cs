using System.Text.Json;
using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

internal static class IntegrationCredentialPresentation
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static bool HasStoredCredential(IntegrationConnectionCredential? r) =>
        r is { ProtectedPayload.Length: > 0 };

    public static IReadOnlyList<string> ParseSecretKeys(string? secretKeysJson)
    {
        if (string.IsNullOrWhiteSpace(secretKeysJson))
        {
            return [];
        }

        try
        {
            var keys = JsonSerializer.Deserialize<List<string>>(secretKeysJson, Json);
            return keys?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<string> LabelsForKeys(IReadOnlyList<string> keys, IntegrationProviderProfile? profile)
    {
        if (profile is null)
        {
            return keys;
        }

        return keys.Select(k =>
        {
            var f = profile.Fields.FirstOrDefault(x => x.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
            return f?.Label ?? k;
        }).ToList();
    }
}
