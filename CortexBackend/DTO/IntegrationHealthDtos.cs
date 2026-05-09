using Cortex.API.Models;

namespace Cortex.API.DTO;

public record IntegrationConnectionHealthDto(
    int ConnectionId,
    IntegrationProvider Provider,
    IntegrationConnectionHealthStatus Status,
    string StatusLabel,
    string Message,
    DateTime? LastTestedAtUtc,
    bool CredentialConfigured,
    IReadOnlyList<string> MissingRequiredSettingKeys,
    IReadOnlyList<string> InvalidFormatSettingKeys,
    IReadOnlyList<string> MissingCredentialFieldKeys,
    bool CanRunLiveTest,
    IntegrationConnectionTestMode TestMode);

public record TestIntegrationConnectionResponse(
    IntegrationConnectionHealthDto Health,
    bool TestSucceeded);
