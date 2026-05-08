using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services.Integrations;
using Microsoft.EntityFrameworkCore;
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
    IOptions<SharePointGraphOptions> sharePointGraphOptions) : IIntegrationCredentialAdminService
{
    private readonly CortexDbContext _db = db;
    private readonly IIntegrationCredentialStore _credentialStore = credentialStore;
    private readonly SharePointGraphOptions _spo = sharePointGraphOptions.Value;

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

        await _credentialStore.MergeAndPersistAsync(connectionId, conn.Provider, conn.AuthMode, patch, cancellationToken);
        var status = await GetStatusAsync(connectionId, cancellationToken);
        return new ConfigureIntegrationCredentialResponse(status!);
    }

    public async Task<ClearIntegrationCredentialResponse?> ClearAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var conn = await _db.IntegrationConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (conn is null)
        {
            return null;
        }

        await _credentialStore.ClearAsync(connectionId, cancellationToken);
        var status = await GetStatusAsync(connectionId, cancellationToken);
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
}
