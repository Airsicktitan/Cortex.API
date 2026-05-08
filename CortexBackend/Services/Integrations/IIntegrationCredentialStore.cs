using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>Persist and decrypt integration secrets. DTO layer must never expose decrypted values.</summary>
public interface IIntegrationCredentialStore
{
    Task<IReadOnlyDictionary<string, string>?> GetDecryptedSecretsAsync(int connectionId, CancellationToken cancellationToken = default);

    /// <summary>Merges patch into existing secrets; empty values remove keys. Removes credential row if result empty.</summary>
    Task MergeAndPersistAsync(
        int connectionId,
        IntegrationProvider provider,
        IntegrationAuthMode authMode,
        IReadOnlyDictionary<string, string> patch,
        CancellationToken cancellationToken = default);

    Task ClearAsync(int connectionId, CancellationToken cancellationToken = default);
}
