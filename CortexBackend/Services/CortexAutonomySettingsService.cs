using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public sealed class CortexAutonomySettingsService(
    CortexDbContext dbContext,
    IOptions<CortexAutonomyOptions> defaults) : ICortexAutonomySettingsService
{
    private readonly CortexDbContext _dbContext = dbContext;
    private readonly CortexAutonomyOptions _defaults = defaults.Value;

    public async Task<CortexAutonomyOptions> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAsync(cancellationToken);
        return Merge(_defaults, stored);
    }

    public Task<CortexAutonomyConfiguration?> GetStoredAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.CortexAutonomyConfigurations
            .AsNoTracking()
            .Include(c => c.LastModifiedByUser)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CortexAutonomyConfiguration> UpdateAsync(
        UpdateCortexAutonomySettingsRequest request,
        int actingUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _dbContext.CortexAutonomyConfigurations
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new CortexAutonomyConfiguration
            {
                Enabled = _defaults.Enabled,
                ShadowMode = _defaults.ShadowMode,
                MinConfidence = _defaults.MinConfidence,
                RecentOverrideWindowHours = _defaults.RecentOverrideWindowHours,
                RequireClearWinner = _defaults.RequireClearWinner,
                MinAlternativeGap = _defaults.MinAlternativeGap,
            };
            _dbContext.CortexAutonomyConfigurations.Add(entity);
        }

        if (request.Enabled.HasValue)
        {
            entity.Enabled = request.Enabled.Value;
        }
        if (request.ShadowMode.HasValue)
        {
            entity.ShadowMode = request.ShadowMode.Value;
        }
        if (request.MinConfidence.HasValue)
        {
            entity.MinConfidence = ClampConfidence(request.MinConfidence.Value);
        }
        if (request.RecentOverrideWindowHours.HasValue)
        {
            entity.RecentOverrideWindowHours = Math.Clamp(request.RecentOverrideWindowHours.Value, 0, 24 * 30);
        }
        if (request.RequireClearWinner.HasValue)
        {
            entity.RequireClearWinner = request.RequireClearWinner.Value;
        }
        if (request.MinAlternativeGap.HasValue)
        {
            entity.MinAlternativeGap = ClampGap(request.MinAlternativeGap.Value);
        }

        entity.LastModifiedBy = actingUserId;
        entity.LastModifiedDateUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return a fresh tracked snapshot with the user navigation included.
        return await _dbContext.CortexAutonomyConfigurations
            .AsNoTracking()
            .Include(c => c.LastModifiedByUser)
            .FirstAsync(c => c.Id == entity.Id, cancellationToken);
    }

    private static CortexAutonomyOptions Merge(
        CortexAutonomyOptions defaults,
        CortexAutonomyConfiguration? stored)
    {
        if (stored is null)
        {
            return new CortexAutonomyOptions
            {
                Enabled = defaults.Enabled,
                ShadowMode = defaults.ShadowMode,
                MinConfidence = defaults.MinConfidence,
                RecentOverrideWindowHours = defaults.RecentOverrideWindowHours,
                RequireClearWinner = defaults.RequireClearWinner,
                MinAlternativeGap = defaults.MinAlternativeGap,
            };
        }

        return new CortexAutonomyOptions
        {
            Enabled = stored.Enabled,
            ShadowMode = stored.ShadowMode,
            MinConfidence = ClampConfidence(stored.MinConfidence),
            RecentOverrideWindowHours = Math.Clamp(stored.RecentOverrideWindowHours, 0, 24 * 30),
            RequireClearWinner = stored.RequireClearWinner,
            MinAlternativeGap = ClampGap(stored.MinAlternativeGap),
        };
    }

    private static double ClampConfidence(double value) => Math.Clamp(value, 0d, 1d);

    private static double ClampGap(double value) => Math.Clamp(value, 0d, 1d);
}
