namespace Cortex.API.Models;

/// <summary>
/// Encrypted per-connection integration secrets. Never store plaintext; never expose payload to clients.
/// </summary>
public sealed class IntegrationConnectionCredential
{
    public int Id { get; set; }

    public int IntegrationConnectionId { get; set; }

    public IntegrationProvider Provider { get; set; }

    /// <summary>Data Protection encrypted UTF-8 JSON of secret key/value pairs.</summary>
    public byte[] ProtectedPayload { get; set; } = [];

    /// <summary>JSON array of secret key names (e.g. ["apiToken"]). Never values.</summary>
    public string SecretKeysJson { get; set; } = "[]";

    public IntegrationAuthMode AuthModeSnapshot { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastRotatedAtUtc { get; set; }

    public DateTime? LastValidatedAtUtc { get; set; }

    public IntegrationConnection IntegrationConnection { get; set; } = null!;
}
