namespace Cortex.API.DTO;

public sealed class DecisionImpactResponse
{
    public bool HasImpact { get; set; }

    public string PreviousRiskLevel { get; set; } = string.Empty;

    public string CurrentRiskLevel { get; set; } = string.Empty;

    public bool RiskImproved { get; set; }

    public decimal PreviousOwnerWorkload { get; set; }

    public decimal CurrentOwnerWorkload { get; set; }

    public bool WorkloadImproved { get; set; }

    public string PreviousPressureLevel { get; set; } = string.Empty;

    public string CurrentPressureLevel { get; set; } = string.Empty;

    public bool PressureImproved { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTime AppliedAtUtc { get; set; }

    public string Source { get; set; } = string.Empty;
}
