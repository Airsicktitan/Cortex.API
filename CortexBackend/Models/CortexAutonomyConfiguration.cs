namespace Cortex.API.Models;

/// <summary>
/// Single-row runtime configuration for the Tier 8 Safe Autonomy Layer.
/// Falls back to <see cref="Cortex.API.Configuration.CortexAutonomyOptions"/> defaults
/// when the row is absent. Editable from the admin Autonomy control panel.
/// </summary>
public class CortexAutonomyConfiguration
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public bool ShadowMode { get; set; } = true;
    public double MinConfidence { get; set; } = 0.85;
    public int RecentOverrideWindowHours { get; set; } = 24;
    public bool RequireClearWinner { get; set; } = true;
    public double MinAlternativeGap { get; set; } = 0.08;
    public int? LastModifiedBy { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }

    public User? LastModifiedByUser { get; set; }
}
