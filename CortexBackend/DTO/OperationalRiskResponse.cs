namespace Cortex.API.DTO;

public sealed class OperationalRiskResponse
{
    public int OperationalRiskScore { get; set; }

    /// <summary>low | moderate | high | critical</summary>
    public string RiskLevel { get; set; } = "low";

    public List<string> Reasons { get; set; } = [];

    public string RecommendedAction { get; set; } = "No immediate intervention required.";

    public OwnerPressureResponse OwnerPressure { get; set; } = new();

    public bool IsAssignmentSafe { get; set; }

    public bool IsOwnerOverloaded { get; set; }

    public bool IsOwnershipComplete { get; set; }
}

public sealed class OwnerPressureResponse
{
    public int WorkloadScore { get; set; }

    /// <summary>low | moderate | high | critical</summary>
    public string PressureLevel { get; set; } = "low";
}
