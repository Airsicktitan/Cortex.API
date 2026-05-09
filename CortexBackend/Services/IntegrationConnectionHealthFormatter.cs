using Cortex.API.Configuration;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services.Integrations;

namespace Cortex.API.Services;

/// <summary>Computes connection health DTOs from persisted rows (no side effects).</summary>
public static class IntegrationConnectionHealthFormatter
{
    public static IntegrationConnectionHealthDto Build(
        IntegrationConnection c,
        IntegrationConnectionCredential? cred,
        SharePointGraphOptions spoOptions)
    {
        var profile = IntegrationProviderCatalog.Get(c.Provider);
        var safeMap = IntegrationConnectionConfigValidator.ToSafeDisplayMap(c, profile);
        var (missingRequired, invalidFormat) = IntegrationConnectionConfigValidator.ValidateNonSecretSettingsSoft(safeMap, profile);
        var missingCredKeys = ComputeMissingCredentialFieldKeys(c, cred, profile);
        var canRunLive = c.Provider == IntegrationProvider.SharePoint && IsSharePointGraphAppConfigured(c, spoOptions);
        var credentialSatisfied = IsCredentialSatisfied(c, cred, missingCredKeys, spoOptions);

        IntegrationConnectionHealthStatus status;
        string message;
        IntegrationConnectionTestMode testMode;

        if (missingRequired.Count > 0 || invalidFormat.Count > 0)
        {
            status = IntegrationConnectionHealthStatus.NotConfigured;
            message = invalidFormat.Count > 0
                ? "Required connection settings are missing or invalid."
                : "Required connection settings are missing.";
            testMode = IntegrationConnectionTestMode.LocalValidation;
        }
        else if (!credentialSatisfied)
        {
            status = IntegrationConnectionHealthStatus.MissingCredentials;
            message = "Credential is required before this connection can be tested.";
            testMode = IntegrationConnectionTestMode.LocalValidation;
        }
        else if (c.LastConnectionTestAtUtc is null)
        {
            status = IntegrationConnectionHealthStatus.NotTested;
            message = "This connection has not been tested yet.";
            testMode = SuggestedTestMode(c.Provider, canRunLive);
        }
        else if (TryParseStoredStatus(c.LastConnectionTestHealthStatus, out var stored))
        {
            status = stored;
            message = string.IsNullOrWhiteSpace(c.LastConnectionTestMessage)
                ? StatusLabelFor(status)
                : c.LastConnectionTestMessage!.Trim();
            testMode = TryParseStoredTestMode(c.LastConnectionTestMode)
                       ?? SuggestedTestMode(c.Provider, canRunLive);
        }
        else
        {
            status = IntegrationConnectionHealthStatus.NotTested;
            message = string.IsNullOrWhiteSpace(c.LastConnectionTestMessage)
                ? "This connection has not been tested yet."
                : c.LastConnectionTestMessage!.Trim();
            testMode = TryParseStoredTestMode(c.LastConnectionTestMode)
                       ?? SuggestedTestMode(c.Provider, canRunLive);
        }

        return new IntegrationConnectionHealthDto(
            c.Id,
            c.Provider,
            status,
            StatusLabelFor(status),
            message,
            c.LastConnectionTestAtUtc,
            credentialSatisfied,
            missingRequired,
            invalidFormat,
            missingCredKeys,
            canRunLive,
            testMode);
    }

    private static IntegrationConnectionTestMode SuggestedTestMode(IntegrationProvider provider, bool sharePointCanRunLive) =>
        provider switch
        {
            IntegrationProvider.Jira or IntegrationProvider.ServiceNow => IntegrationConnectionTestMode.NotAvailable,
            IntegrationProvider.SapReference => IntegrationConnectionTestMode.LocalValidation,
            IntegrationProvider.SharePoint when sharePointCanRunLive => IntegrationConnectionTestMode.LiveProviderValidation,
            _ => IntegrationConnectionTestMode.LocalValidation,
        };

    private static IntegrationConnectionTestMode? TryParseStoredTestMode(string? s)
    {
        if (!string.IsNullOrWhiteSpace(s) &&
            Enum.TryParse<IntegrationConnectionTestMode>(s, ignoreCase: false, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool TryParseStoredStatus(string? s, out IntegrationConnectionHealthStatus status)
    {
        if (!string.IsNullOrWhiteSpace(s) &&
            Enum.TryParse<IntegrationConnectionHealthStatus>(s, ignoreCase: false, out var parsed))
        {
            status = parsed;
            return true;
        }

        status = default;
        return false;
    }

    internal static bool IsCredentialSatisfied(
        IntegrationConnection c,
        IntegrationConnectionCredential? cred,
        IReadOnlyList<string> missingSecretKeys,
        SharePointGraphOptions spoOptions)
    {
        if (c.Provider == IntegrationProvider.SharePoint)
        {
            return IsSharePointGraphAppConfigured(c, spoOptions) ||
                   IntegrationCredentialPresentation.HasStoredCredential(cred);
        }

        return missingSecretKeys.Count == 0;
    }

    internal static bool IsSharePointGraphAppConfigured(IntegrationConnection connection, SharePointGraphOptions options)
    {
        var tenant = connection.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenant))
        {
            tenant = options.TenantId?.Trim();
        }

        var clientId = options.ClientId?.Trim();
        var clientSecret = options.ClientSecret?.Trim();
        return !string.IsNullOrEmpty(tenant)
               && !string.IsNullOrEmpty(clientId)
               && !string.IsNullOrEmpty(clientSecret);
    }

    internal static IReadOnlyList<string> ComputeMissingCredentialFieldKeys(
        IntegrationConnection c,
        IntegrationConnectionCredential? cred,
        IntegrationProviderProfile profile)
    {
        _ = profile;
        var stored = IntegrationCredentialPresentation.ParseSecretKeys(cred?.SecretKeysJson);
        var storedSet = new HashSet<string>(stored, StringComparer.OrdinalIgnoreCase);

        switch (c.Provider)
        {
            case IntegrationProvider.SharePoint:
                return [];

            case IntegrationProvider.Jira:
                if (c.AuthMode == IntegrationAuthMode.ApiToken && !storedSet.Contains("apiToken"))
                {
                    return ["apiToken"];
                }

                break;

            case IntegrationProvider.ServiceNow:
                if (c.AuthMode == IntegrationAuthMode.OAuthClientCredentials && !storedSet.Contains("clientSecret"))
                {
                    return ["clientSecret"];
                }

                if (c.AuthMode == IntegrationAuthMode.ApiToken && !storedSet.Contains("apiToken"))
                {
                    return ["apiToken"];
                }

                break;
        }

        return [];
    }

    public static string StatusLabelFor(IntegrationConnectionHealthStatus s) =>
        s switch
        {
            IntegrationConnectionHealthStatus.NotConfigured => "Not configured",
            IntegrationConnectionHealthStatus.MissingCredentials => "Missing credentials",
            IntegrationConnectionHealthStatus.NotTested => "Not tested",
            IntegrationConnectionHealthStatus.Healthy => "Healthy",
            IntegrationConnectionHealthStatus.NeedsAttention => "Needs attention",
            IntegrationConnectionHealthStatus.TestUnavailable => "Test unavailable",
            _ => s.ToString(),
        };
}
