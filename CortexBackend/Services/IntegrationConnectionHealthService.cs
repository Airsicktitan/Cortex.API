using System.Text.Json;
using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public interface IIntegrationConnectionHealthService
{
    Task<IntegrationConnectionHealthDto?> GetHealthAsync(int connectionId, CancellationToken cancellationToken = default);

    Task<TestIntegrationConnectionResponse?> TestConnectionAsync(int connectionId, CancellationToken cancellationToken = default);
}

public sealed class IntegrationConnectionHealthService(
    CortexDbContext db,
    ISharePointGraphClient sharePointGraph,
    IOptions<SharePointGraphOptions> sharePointOptions,
    IIntegrationActivityService activityService,
    IUserContextService userContext,
    ILogger<IntegrationConnectionHealthService> logger) : IIntegrationConnectionHealthService
{
    private readonly CortexDbContext _db = db;
    private readonly ISharePointGraphClient _sharePointGraph = sharePointGraph;
    private readonly SharePointGraphOptions _spo = sharePointOptions.Value;
    private readonly IIntegrationActivityService _activity = activityService;
    private readonly IUserContextService _userContext = userContext;
    private readonly ILogger<IntegrationConnectionHealthService> _logger = logger;

    public async Task<IntegrationConnectionHealthDto?> GetHealthAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(connectionId, tracking: false, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        return IntegrationConnectionHealthFormatter.Build(row.Value.Connection, row.Value.Credential, _spo);
    }

    public async Task<TestIntegrationConnectionResponse?> TestConnectionAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(connectionId, tracking: true, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var conn = row.Value.Connection;
        var cred = row.Value.Credential;
        var profile = IntegrationProviderCatalog.Get(conn.Provider);
        var safeMap = IntegrationConnectionConfigValidator.ToSafeDisplayMap(conn, profile);
        var (missingRequired, invalidFormat) = IntegrationConnectionConfigValidator.ValidateNonSecretSettingsSoft(safeMap, profile);
        var missingCredKeys = IntegrationConnectionHealthFormatter.ComputeMissingCredentialFieldKeys(conn, cred, profile);
        var credentialSatisfied = IntegrationConnectionHealthFormatter.IsCredentialSatisfied(conn, cred, missingCredKeys, _spo);

        IntegrationConnectionHealthStatus status;
        string message;
        var testMode = IntegrationConnectionTestMode.LocalValidation;
        var success = false;
        var now = DateTime.UtcNow;

        if (missingRequired.Count > 0 || invalidFormat.Count > 0)
        {
            status = IntegrationConnectionHealthStatus.NotConfigured;
            message = invalidFormat.Count > 0
                ? "Required connection settings are missing or invalid."
                : "Required connection settings are missing.";
            success = false;
        }
        else if (!credentialSatisfied)
        {
            status = IntegrationConnectionHealthStatus.MissingCredentials;
            message = "Credential is required before this connection can be tested.";
            success = false;
        }
        else
        {
            switch (conn.Provider)
            {
                case IntegrationProvider.SharePoint:
                    if (IntegrationConnectionHealthFormatter.IsSharePointGraphAppConfigured(conn, _spo))
                    {
                        testMode = IntegrationConnectionTestMode.LiveProviderValidation;
                        try
                        {
                            await _sharePointGraph.ValidateGraphApplicationCredentialsAsync(conn.TenantId, cancellationToken)
                                .ConfigureAwait(false);
                            status = IntegrationConnectionHealthStatus.Healthy;
                            message = "Connection settings passed validation.";
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            status = IntegrationConnectionHealthStatus.NeedsAttention;
                            message = IntegrationHealthMessageSanitizer.SanitizeForConnectionTest(ex);
                            success = false;
                            _logger.LogWarning(ex, "SharePoint connection test failed for connection {ConnectionId}.", connectionId);
                        }
                    }
                    else
                    {
                        testMode = IntegrationConnectionTestMode.LocalValidation;
                        status = IntegrationConnectionHealthStatus.TestUnavailable;
                        message =
                            "SharePoint settings were checked locally. Microsoft Graph application credentials are not fully configured for live validation.";
                        success = true;
                    }

                    break;

                case IntegrationProvider.Jira:
                    testMode = IntegrationConnectionTestMode.NotAvailable;
                    status = IntegrationConnectionHealthStatus.TestUnavailable;
                    message =
                        "Jira connection settings are configured. Live Jira validation is not enabled yet.";
                    success = true;
                    break;

                case IntegrationProvider.ServiceNow:
                    testMode = IntegrationConnectionTestMode.NotAvailable;
                    status = IntegrationConnectionHealthStatus.TestUnavailable;
                    message =
                        "ServiceNow connection settings are configured. Live ServiceNow validation is not enabled yet.";
                    success = true;
                    break;

                case IntegrationProvider.SapReference:
                    testMode = IntegrationConnectionTestMode.LocalValidation;
                    status = IntegrationConnectionHealthStatus.TestUnavailable;
                    message =
                        "SAP Reference uses stored metadata only. Live SAP validation is not configured.";
                    success = true;
                    break;

                default:
                    testMode = IntegrationConnectionTestMode.NotAvailable;
                    status = IntegrationConnectionHealthStatus.TestUnavailable;
                    message = "Configuration checked locally.";
                    success = true;
                    break;
            }
        }

        conn.LastConnectionTestAtUtc = now;
        conn.LastConnectionTestHealthStatus = status.ToString();
        conn.LastConnectionTestMode = testMode.ToString();
        conn.LastConnectionTestMessage = message;
        conn.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryRecordTestActivityAsync(
            connectionId,
            success,
            testMode,
            status,
            credentialSatisfied,
            missingRequired,
            invalidFormat,
            missingCredKeys,
            message,
            cancellationToken).ConfigureAwait(false);

        var refreshed = await LoadRowAsync(connectionId, tracking: false, cancellationToken).ConfigureAwait(false);
        var dto = IntegrationConnectionHealthFormatter.Build(refreshed!.Value.Connection, refreshed.Value.Credential, _spo);
        return new TestIntegrationConnectionResponse(dto, success);
    }

    private async Task<(IntegrationConnection Connection, IntegrationConnectionCredential? Credential)?> LoadRowAsync(
        int connectionId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var q = tracking
            ? _db.IntegrationConnections.AsQueryable()
            : _db.IntegrationConnections.AsNoTracking();

        var conn = await q.FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken).ConfigureAwait(false);
        if (conn is null)
        {
            return null;
        }

        var cred = await _db.IntegrationConnectionCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IntegrationConnectionId == connectionId, cancellationToken)
            .ConfigureAwait(false);

        return (conn, cred);
    }

    private async Task TryRecordTestActivityAsync(
        int connectionId,
        bool success,
        IntegrationConnectionTestMode testMode,
        IntegrationConnectionHealthStatus healthStatus,
        bool credentialConfigured,
        IReadOnlyList<string> missingRequiredKeys,
        IReadOnlyList<string> invalidFormatKeys,
        IReadOnlyList<string> missingCredentialKeys,
        string safeMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await TryGetActorAsync().ConfigureAwait(false);
            var meta = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["success"] = success,
                    ["testMode"] = testMode.ToString(),
                    ["healthStatus"] = healthStatus.ToString(),
                    ["credentialConfigured"] = credentialConfigured,
                    ["missingRequiredSettingKeys"] = missingRequiredKeys.ToArray(),
                    ["invalidFormatSettingKeys"] = invalidFormatKeys.ToArray(),
                    ["missingCredentialFieldKeys"] = missingCredentialKeys.ToArray(),
                },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var now = DateTime.UtcNow;
            await _activity.RecordAsync(
                new IntegrationActivityLogRecordRequest
                {
                    ExternalWorkSourceId = null,
                    IntegrationConnectionId = connectionId,
                    ActivityType = IntegrationActivityType.ConnectionTested,
                    Status = success ? IntegrationActivityStatus.Success : IntegrationActivityStatus.Failed,
                    TriggeredByUserId = actor?.Id,
                    TriggeredByDisplayName = actor?.DisplayName,
                    TriggeredByEmail = string.IsNullOrEmpty(actor?.Email) ? null : actor?.Email,
                    StartedAtUtc = now,
                    CompletedAtUtc = now,
                    Message = Trim(safeMessage, 2000),
                    MetadataJson = Trim(meta, 2000),
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record connection test activity for connection {ConnectionId}.", connectionId);
        }
    }

    private static string? Trim(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s[..max];

    private async Task<(int Id, string DisplayName, string Email)?> TryGetActorAsync()
    {
        try
        {
            var u = await _userContext.GetCurrentUserAsync().ConfigureAwait(false);
            var email = u.Email ?? "";
            return (u.Id, u.DisplayName ?? email, email);
        }
        catch
        {
            return null;
        }
    }
}
