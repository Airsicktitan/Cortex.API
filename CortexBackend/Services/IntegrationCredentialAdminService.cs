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

public interface IIntegrationCredentialAdminService
{
    Task<IntegrationCredentialStatusDto?> GetStatusAsync(int connectionId, CancellationToken cancellationToken = default);

    Task<ConfigureIntegrationCredentialResponse?> ConfigureAsync(
        int connectionId,
        ConfigureIntegrationCredentialRequest request,
        CancellationToken cancellationToken = default);

    Task<ClearIntegrationCredentialResponse?> ClearAsync(int connectionId, CancellationToken cancellationToken = default);
}

public sealed class IntegrationCredentialAdminService(
    CortexDbContext db,
    IIntegrationCredentialStore credentialStore,
    IIntegrationActivityService integrationActivity,
    IUserContextService userContext,
    IOptions<SharePointGraphOptions> sharePointGraphOptions,
    ILogger<IntegrationCredentialAdminService> logger) : IIntegrationCredentialAdminService
{
    private readonly CortexDbContext _db = db;
    private readonly IIntegrationCredentialStore _credentialStore = credentialStore;
    private readonly IIntegrationActivityService _integrationActivity = integrationActivity;
    private readonly IUserContextService _userContext = userContext;
    private readonly SharePointGraphOptions _spo = sharePointGraphOptions.Value;
    private readonly ILogger<IntegrationCredentialAdminService> _logger = logger;

    public async Task<IntegrationCredentialStatusDto?> GetStatusAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var conn = await _db.IntegrationConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (conn is null)
        {
            return null;
        }

        var cred = await _db.IntegrationConnectionCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IntegrationConnectionId == connectionId, cancellationToken);
        return MapStatus(conn, cred);
    }

    public async Task<ConfigureIntegrationCredentialResponse?> ConfigureAsync(
        int connectionId,
        ConfigureIntegrationCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        var conn = await _db.IntegrationConnections.FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (conn is null)
        {
            return null;
        }

        var hadStoredCredentialRow = await _db.IntegrationConnectionCredentials.AsNoTracking()
            .AnyAsync(x => x.IntegrationConnectionId == connectionId, cancellationToken);

        IntegrationCredentialSecretValidator.ValidateSecretsForConfigure(conn.Provider, request.Secrets);
        var patch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in request.Secrets!)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            patch[kv.Key.Trim()] = kv.Value?.Trim() ?? string.Empty;
        }

        var submittedKeyNames = patch
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await _credentialStore.MergeAndPersistAsync(connectionId, conn.Provider, conn.AuthMode, patch, cancellationToken);
        var status = await GetStatusAsync(connectionId, cancellationToken);

        var activityType = hadStoredCredentialRow
            ? IntegrationActivityType.CredentialRotated
            : IntegrationActivityType.CredentialConfigured;
        var message = hadStoredCredentialRow ? "Credential rotated" : "Credential configured";
        var metadata = BuildCredentialAuditMetadata(
            conn,
            submittedKeyNames,
            status?.CredentialConfigured ?? false);

        await TryRecordCredentialActivityAsync(
            connectionId,
            activityType,
            message,
            metadata,
            cancellationToken);

        return new ConfigureIntegrationCredentialResponse(status!);
    }

    public async Task<ClearIntegrationCredentialResponse?> ClearAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var conn = await _db.IntegrationConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (conn is null)
        {
            return null;
        }

        var credRow = await _db.IntegrationConnectionCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IntegrationConnectionId == connectionId, cancellationToken);
        var hadStored = IntegrationCredentialPresentation.HasStoredCredential(credRow);

        await _credentialStore.ClearAsync(connectionId, cancellationToken);
        var status = await GetStatusAsync(connectionId, cancellationToken);

        if (hadStored)
        {
            var metadata = BuildCredentialAuditMetadata(conn, [], status?.CredentialConfigured ?? false);
            await TryRecordCredentialActivityAsync(
                connectionId,
                IntegrationActivityType.CredentialCleared,
                "Credential cleared",
                metadata,
                cancellationToken);
        }

        return new ClearIntegrationCredentialResponse(status!);
    }

    private IntegrationCredentialStatusDto MapStatus(IntegrationConnection conn, IntegrationConnectionCredential? cred)
    {
        var profile = IntegrationProviderCatalog.TryGet(conn.Provider);
        var hasStored = IntegrationCredentialPresentation.HasStoredCredential(cred);
        var (configured, credType) = ResolveIndicators(conn, hasStored);
        var keys = IntegrationCredentialPresentation.ParseSecretKeys(cred?.SecretKeysJson);
        var labels = IntegrationCredentialPresentation.LabelsForKeys(keys, profile);
        var status = configured ? "Configured" : "NotConfigured";
        return new IntegrationCredentialStatusDto(
            conn.Id,
            conn.Provider,
            configured,
            status,
            labels,
            conn.AuthMode,
            credType,
            cred?.CreatedAtUtc,
            cred?.LastRotatedAtUtc,
            cred?.LastValidatedAtUtc);
    }

    private (bool Configured, string? Type) ResolveIndicators(IntegrationConnection c, bool hasStoredCredential)
    {
        if (c.Provider == IntegrationProvider.SharePoint)
        {
            var hasApp = !string.IsNullOrWhiteSpace(_spo.ClientSecret) &&
                         !string.IsNullOrWhiteSpace(_spo.ClientId);
            var hasTenant = !string.IsNullOrWhiteSpace(c.TenantId);
            var globalOk = hasApp && hasTenant;
            if (globalOk || hasStoredCredential)
            {
                return (true, globalOk ? "MicrosoftGraphAppRegistration" : "ConnectionCredential");
            }

            return (false, null);
        }

        if (hasStoredCredential)
        {
            return (true, "ConnectionCredential");
        }

        return (false, null);
    }

    private static string? BuildCredentialAuditMetadata(
        IntegrationConnection conn,
        IReadOnlyList<string> updatedSecretKeys,
        bool credentialConfiguredAfter)
    {
        var d = new Dictionary<string, object?>
        {
            ["connectionId"] = conn.Id,
            ["connectionDisplayName"] = conn.DisplayName,
            ["provider"] = conn.Provider.ToString(),
            ["authMode"] = conn.AuthMode.ToString(),
            ["credentialConfigured"] = credentialConfiguredAfter,
            ["updatedSecretKeys"] = updatedSecretKeys,
        };

        return JsonSerializer.Serialize(d, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task TryRecordCredentialActivityAsync(
        int connectionId,
        IntegrationActivityType activityType,
        string message,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await TryGetActorAsync();
            var now = DateTime.UtcNow;
            await _integrationActivity.RecordAsync(
                new IntegrationActivityLogRecordRequest
                {
                    ExternalWorkSourceId = null,
                    IntegrationConnectionId = connectionId,
                    ActivityType = activityType,
                    Status = IntegrationActivityStatus.Success,
                    TriggeredByUserId = actor?.Id,
                    TriggeredByDisplayName = actor?.DisplayName,
                    TriggeredByEmail = string.IsNullOrEmpty(actor?.Email) ? null : actor?.Email,
                    StartedAtUtc = now,
                    CompletedAtUtc = now,
                    Message = message,
                    MetadataJson = metadataJson,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to record integration credential activity {ActivityType} for connection {ConnectionId}.",
                activityType,
                connectionId);
        }
    }

    private async Task<(int Id, string DisplayName, string Email)?> TryGetActorAsync()
    {
        try
        {
            var u = await _userContext.GetCurrentUserAsync();
            var email = u.Email ?? "";
            return (u.Id, u.DisplayName ?? email, email);
        }
        catch
        {
            return null;
        }
    }
}
