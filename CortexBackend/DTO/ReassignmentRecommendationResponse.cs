namespace Cortex.API.DTO;

public sealed class ReassignmentRecommendationResponse
{
    public bool ShouldSuggestReassignment { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>synitiOwner | businessOwner | unassigned</summary>
    public string AssignmentField { get; set; } = "unassigned";

    public ReassignmentOwnerSnapshotResponse? CurrentOwner { get; set; }

    public List<ReassignmentTargetResponse> SuggestedTargets { get; set; } = [];
}

public class ReassignmentOwnerSnapshotResponse
{
    public int? UserId { get; set; }

    public string OwnerKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int WorkloadScore { get; set; }

    /// <summary>low | moderate | high | critical</summary>
    public string PressureLevel { get; set; } = "low";
}

public sealed class ReassignmentTargetResponse : ReassignmentOwnerSnapshotResponse
{
    public bool IsBetterThanCurrent { get; set; }

    public string ImprovementReason { get; set; } = string.Empty;
}
