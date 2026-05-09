namespace Cortex.API.Models;

/// <summary>High-level health for an integration connection (derived + last test).</summary>
public enum IntegrationConnectionHealthStatus
{
    NotConfigured,
    MissingCredentials,
    NotTested,
    Healthy,
    NeedsAttention,
    TestUnavailable,
}

/// <summary>How the latest connection test was executed.</summary>
public enum IntegrationConnectionTestMode
{
    LocalValidation,
    LiveProviderValidation,
    NotAvailable,
}
