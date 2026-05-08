using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services.Integrations;

public sealed class EncryptedIntegrationCredentialStore(
    CortexDbContext db,
    IDataProtectionProvider dataProtectionProvider) : IIntegrationCredentialStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Cortex.IntegrationCredentials.v1");
    private readonly CortexDbContext _db = db;

    public async Task<IReadOnlyDictionary<string, string>?> GetDecryptedSecretsAsync(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.IntegrationConnectionCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IntegrationConnectionId == connectionId, cancellationToken);
        if (row is not { ProtectedPayload.Length: > 0 })
        {
            return null;
        }

        try
        {
            var plain = _protector.Unprotect(row.ProtectedPayload);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(plain), Json);
            return map;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task MergeAndPersistAsync(
        int connectionId,
        IntegrationProvider provider,
        IntegrationAuthMode authMode,
        IReadOnlyDictionary<string, string> patch,
        CancellationToken cancellationToken = default)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var existing = await GetDecryptedSecretsAsync(connectionId, cancellationToken);
        if (existing != null)
        {
            foreach (var kv in existing)
            {
                merged[kv.Key] = kv.Value;
            }
        }

        foreach (var kv in patch)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(kv.Value))
            {
                merged.Remove(kv.Key.Trim());
            }
            else
            {
                merged[kv.Key.Trim()] = kv.Value.Trim();
            }
        }

        var row = await _db.IntegrationConnectionCredentials.FirstOrDefaultAsync(
            x => x.IntegrationConnectionId == connectionId,
            cancellationToken);
        if (merged.Count == 0)
        {
            if (row != null)
            {
                _db.IntegrationConnectionCredentials.Remove(row);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var payloadJson = JsonSerializer.Serialize(merged, Json);
        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(payloadJson));
        var keysJson = JsonSerializer.Serialize(merged.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList(), Json);

        var now = DateTime.UtcNow;
        if (row is null)
        {
            row = new IntegrationConnectionCredential
            {
                IntegrationConnectionId = connectionId,
                Provider = provider,
                AuthModeSnapshot = authMode,
                ProtectedPayload = protectedBytes,
                SecretKeysJson = keysJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                LastRotatedAtUtc = null,
            };
            _db.IntegrationConnectionCredentials.Add(row);
        }
        else
        {
            row.Provider = provider;
            row.AuthModeSnapshot = authMode;
            row.ProtectedPayload = protectedBytes;
            row.SecretKeysJson = keysJson;
            row.UpdatedAtUtc = now;
            row.LastRotatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var row = await _db.IntegrationConnectionCredentials.FirstOrDefaultAsync(
            x => x.IntegrationConnectionId == connectionId,
            cancellationToken);
        if (row == null)
        {
            return;
        }

        _db.IntegrationConnectionCredentials.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
