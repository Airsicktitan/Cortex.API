using Cortex.API.Models;

namespace Cortex.API.DTO;

public record IntegrationCredentialStatusDto(
    int ConnectionId,
    IntegrationProvider Provider,
    bool CredentialConfigured,
    string CredentialStatus,
    IReadOnlyList<string> ConfiguredSecretFieldLabels,
    IntegrationAuthMode AuthMode,
    string? CredentialType,
    DateTime? LastConfiguredAtUtc,
    DateTime? LastRotatedAtUtc,
    DateTime? LastValidatedAtUtc);

public record ConfigureIntegrationCredentialRequest
{
    /// <summary>Secret keys and values. Empty string removes that key from stored secrets.</summary>
    public Dictionary<string, string?>? Secrets { get; init; }
}

public record ConfigureIntegrationCredentialResponse(IntegrationCredentialStatusDto Status);

public record ClearIntegrationCredentialResponse(IntegrationCredentialStatusDto Status);
