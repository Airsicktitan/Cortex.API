using System.Text.Json;
using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>Builds and validates normalized connection settings from API requests and provider profiles.</summary>
public static class IntegrationConnectionConfigValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public sealed class NormalizedSettings
    {
        public string? TenantId { get; init; }
        public string? OrganizationId { get; init; }
        public string? PublicSettingsJson { get; init; }
        public IntegrationAuthMode AuthMode { get; init; }
        public IntegrationSyncMode SyncMode { get; init; }
    }

    public static NormalizedSettings ValidateAndNormalizeCreate(CreateIntegrationConnectionRequest request)
    {
        var profile = IntegrationProviderCatalog.Get(request.Provider);
        var auth = request.AuthMode ?? DefaultAuthMode(profile);
        var sync = request.SyncMode ?? IntegrationSyncMode.ReadOnly;
        EnsureAuthAndSync(profile, auth, sync);
        var merged = MergeDictionary(request, request.Provider);
        ThrowIfSecretPayloadInMerged(merged, profile);
        ApplyRequiredAndFormats(merged, profile);
        return SplitColumnsAndJson(request.Provider, profile, merged, auth, sync);
    }

    public static NormalizedSettings ValidateAndNormalizeUpdate(
        IntegrationProvider provider,
        UpdateIntegrationConnectionRequest request,
        IntegrationConnection existing)
    {
        var profile = IntegrationProviderCatalog.Get(provider);
        var auth = request.AuthMode ?? existing.AuthMode;
        var sync = request.SyncMode ?? existing.SyncMode;
        EnsureAuthAndSync(profile, auth, sync);
        var merged = MergeDictionary(request, existing, provider);
        ThrowIfSecretPayloadInMerged(merged, profile);
        ApplyRequiredAndFormats(merged, profile);
        return SplitColumnsAndJson(provider, profile, merged, auth, sync);
    }

    private static IntegrationAuthMode DefaultAuthMode(IntegrationProviderProfile profile) =>
        profile.Provider == IntegrationProvider.SapReference
            ? IntegrationAuthMode.ReferenceMetadata
            : IntegrationAuthMode.Manual;

    private static void EnsureAuthAndSync(
        IntegrationProviderProfile profile,
        IntegrationAuthMode auth,
        IntegrationSyncMode sync)
    {
        if (!profile.AllowedAuthModes.Contains(auth))
        {
            throw new IntegrationApiException(400, "The selected authentication mode is not enabled for this provider.");
        }

        if (!profile.AllowedSyncModes.Contains(sync))
        {
            throw new IntegrationApiException(400, "The selected sync mode is not enabled for this provider.");
        }
    }

    private static Dictionary<string, string> MergeDictionary(CreateIntegrationConnectionRequest request, IntegrationProvider provider)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.ProviderSettings != null)
        {
            foreach (var kv in request.ProviderSettings)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    continue;
                }

                d[kv.Key.Trim()] = kv.Value?.Trim() ?? string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.TenantId))
        {
            d["tenantId"] = request.TenantId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.OrganizationId) && provider == IntegrationProvider.SharePoint)
        {
            d["siteUrl"] = request.OrganizationId.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(request.OrganizationId) && provider != IntegrationProvider.SharePoint)
        {
            // Legacy: OrganizationId was generic; only map to SharePoint site. Other providers use ProviderSettings.
            throw new IntegrationApiException(
                400,
                "Use provider-specific settings instead of the legacy organization field for this provider.");
        }

        return d;
    }

    private static Dictionary<string, string> MergeDictionary(
        UpdateIntegrationConnectionRequest request,
        IntegrationConnection existing,
        IntegrationProvider provider)
    {
        var existingDict = ParseExisting(existing.PublicSettingsJson);
        if (request.ProviderSettings != null)
        {
            foreach (var kv in request.ProviderSettings)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    continue;
                }

                if (kv.Value is null)
                {
                    existingDict.Remove(kv.Key.Trim());
                }
                else
                {
                    existingDict[kv.Key.Trim()] = kv.Value.Trim();
                }
            }
        }

        if (request.TenantId != null)
        {
            if (string.IsNullOrWhiteSpace(request.TenantId))
            {
                existingDict.Remove("tenantId");
            }
            else
            {
                existingDict["tenantId"] = request.TenantId.Trim();
            }
        }

        if (request.OrganizationId != null)
        {
            if (provider != IntegrationProvider.SharePoint)
            {
                if (!string.IsNullOrWhiteSpace(request.OrganizationId))
                {
                    throw new IntegrationApiException(
                        400,
                        "Use provider-specific settings instead of the legacy organization field for this provider.");
                }
            }
            else if (string.IsNullOrWhiteSpace(request.OrganizationId))
            {
                existingDict.Remove("siteUrl");
            }
            else
            {
                existingDict["siteUrl"] = request.OrganizationId.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(existing.TenantId) && !existingDict.ContainsKey("tenantId"))
        {
            existingDict["tenantId"] = existing.TenantId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(existing.OrganizationId) && !existingDict.ContainsKey("siteUrl"))
        {
            existingDict["siteUrl"] = existing.OrganizationId.Trim();
        }

        return existingDict;
    }

    private static Dictionary<string, string> ParseExisting(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
            if (raw is null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
            {
                if (kv.Value.ValueKind == JsonValueKind.String)
                {
                    d[kv.Key] = kv.Value.GetString() ?? string.Empty;
                }
                else
                {
                    d[kv.Key] = kv.Value.ToString();
                }
            }

            return d;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void ThrowIfSecretPayloadInMerged(Dictionary<string, string> merged, IntegrationProviderProfile profile)
    {
        var allowed = new HashSet<string>(profile.Fields.Select(f => f.Key), StringComparer.OrdinalIgnoreCase);
        foreach (var key in merged.Keys.ToList())
        {
            if (!allowed.Contains(key))
            {
                throw new IntegrationApiException(400, $"Unknown or unsupported setting '{key}' for this provider.");
            }
        }

        foreach (var field in profile.Fields.Where(f => f.IsSecret))
        {
            if (merged.TryGetValue(field.Key, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                throw new IntegrationApiException(
                    400,
                    "Credential fields cannot be stored on the connection in this version. Configure secrets using your secure host configuration.");
            }
        }
    }

    private static void ApplyRequiredAndFormats(Dictionary<string, string> merged, IntegrationProviderProfile profile)
    {
        foreach (var field in profile.Fields)
        {
            merged.TryGetValue(field.Key, out var raw);
            var value = raw ?? string.Empty;
            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                throw new IntegrationApiException(400, $"{field.Label} is required for {profile.DisplayName}.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (field.FieldType.Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new IntegrationApiException(400, $"{field.Label} must be a valid http or https URL.");
                }
            }

            if (field.Key.Equals("projectKey", StringComparison.OrdinalIgnoreCase) &&
                profile.Provider == IntegrationProvider.Jira &&
                value.Length > 32)
            {
                throw new IntegrationApiException(400, "Jira project key looks too long.");
            }
        }
    }

    private static NormalizedSettings SplitColumnsAndJson(
        IntegrationProvider provider,
        IntegrationProviderProfile profile,
        Dictionary<string, string> merged,
        IntegrationAuthMode auth,
        IntegrationSyncMode sync)
    {
        string? tenant = null;
        string? org = null;
        var jsonBag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in profile.Fields)
        {
            merged.TryGetValue(field.Key, out var val);
            var s = val ?? string.Empty;
            if (field.MapsToConnectionColumn == nameof(IntegrationConnection.TenantId))
            {
                tenant = string.IsNullOrWhiteSpace(s) ? null : s;
                continue;
            }

            if (field.MapsToConnectionColumn == nameof(IntegrationConnection.OrganizationId))
            {
                org = string.IsNullOrWhiteSpace(s) ? null : s;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(s))
            {
                jsonBag[field.Key] = s;
            }
        }

        // SharePoint-style fallbacks when columns map but keys absent (legacy callers)
        if (provider == IntegrationProvider.SharePoint)
        {
            if (string.IsNullOrWhiteSpace(tenant) && merged.TryGetValue("tenantId", out var t))
            {
                tenant = t;
            }

            if (string.IsNullOrWhiteSpace(org) && merged.TryGetValue("siteUrl", out var u))
            {
                org = u;
            }
        }

        var json = jsonBag.Count > 0 ? JsonSerializer.Serialize(jsonBag, JsonOptions) : null;
        return new NormalizedSettings
        {
            TenantId = tenant,
            OrganizationId = org,
            PublicSettingsJson = json,
            AuthMode = auth,
            SyncMode = sync,
        };
    }

    public static IReadOnlyDictionary<string, string> ToSafeDisplayMap(IntegrationConnection c, IntegrationProviderProfile profile)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in profile.Fields.Where(f => !f.IsSecret))
        {
            if (field.MapsToConnectionColumn == nameof(IntegrationConnection.TenantId) &&
                !string.IsNullOrWhiteSpace(c.TenantId))
            {
                map[field.Key] = c.TenantId.Trim();
            }
            else if (field.MapsToConnectionColumn == nameof(IntegrationConnection.OrganizationId) &&
                     !string.IsNullOrWhiteSpace(c.OrganizationId))
            {
                map[field.Key] = c.OrganizationId.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(c.PublicSettingsJson))
        {
            foreach (var kv in ParseExisting(c.PublicSettingsJson))
            {
                var rule = profile.Fields.FirstOrDefault(f =>
                    f.Key.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                if (rule is { IsSecret: false })
                {
                    map[kv.Key] = kv.Value;
                }
            }
        }

        return map;
    }

    /// <summary>Non-throwing validation for health checks and connection tests.</summary>
    public static (IReadOnlyList<string> MissingRequiredKeys, IReadOnlyList<string> InvalidFormatKeys) ValidateNonSecretSettingsSoft(
        IReadOnlyDictionary<string, string> map,
        IntegrationProviderProfile profile)
    {
        var missing = new List<string>();
        var invalid = new List<string>();
        foreach (var field in profile.Fields.Where(f => !f.IsSecret))
        {
            map.TryGetValue(field.Key, out var raw);
            var value = raw ?? string.Empty;
            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                missing.Add(field.Key);
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (field.FieldType.Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    invalid.Add(field.Key);
                }
            }

            if (field.Key.Equals("projectKey", StringComparison.OrdinalIgnoreCase) &&
                profile.Provider == IntegrationProvider.Jira &&
                value.Length > 32)
            {
                invalid.Add(field.Key);
            }
        }

        return (missing, invalid);
    }
}
