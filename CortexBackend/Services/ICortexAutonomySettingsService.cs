using Cortex.API.Configuration;
using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Reads/writes runtime values for the Tier 8 Safe Autonomy Layer.
/// Persists a single configuration row; falls back to <see cref="CortexAutonomyOptions"/> defaults
/// when no row exists. Used by <see cref="CortexAutonomyService"/> so toggles take effect without restart.
/// </summary>
public interface ICortexAutonomySettingsService
{
    /// <summary>Returns the effective options snapshot (DB values overlaid on bound defaults).</summary>
    Task<CortexAutonomyOptions> GetEffectiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted row or null when absent.</summary>
    Task<CortexAutonomyConfiguration?> GetStoredAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies a partial update; only non-null fields are written. Returns the updated row.</summary>
    Task<CortexAutonomyConfiguration> UpdateAsync(
        UpdateCortexAutonomySettingsRequest request,
        int actingUserId,
        CancellationToken cancellationToken = default);
}
