namespace Cortex.API.Models;

public enum IntegrationActivityType
{
    DiscoverFields,
    SyncSource,
    ManualUpsert,

    /// <summary>First-time per-connection stored credential saved (no prior credential row).</summary>
    CredentialConfigured,

    /// <summary>Existing per-connection stored credential replaced or merged.</summary>
    CredentialRotated,

    /// <summary>Per-connection stored credential removed.</summary>
    CredentialCleared,
}
