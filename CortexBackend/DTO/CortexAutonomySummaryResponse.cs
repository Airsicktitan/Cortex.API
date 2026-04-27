namespace Cortex.API.DTO;

public sealed class CortexAutonomySettingsResponse
{
    public bool Enabled { get; set; }
    public bool ShadowMode { get; set; }
    public double MinConfidence { get; set; }
    public int RecentOverrideWindowHours { get; set; }
    public bool RequireClearWinner { get; set; }
    public double MinAlternativeGap { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
    public string? LastModifiedByDisplayName { get; set; }

    /// <summary>One of: Disabled, Shadow, Active.</summary>
    public string Mode { get; set; } = "Disabled";
}

public sealed class CortexAutonomyCountsResponse
{
    public int Evaluated { get; set; }
    public int Eligible { get; set; }
    public int AutoApplied { get; set; }
    public int Blocked { get; set; }
}

public sealed class CortexAutonomyRecentDecisionResponse
{
    public string TicketId { get; set; } = string.Empty;
    public string? TicketTitle { get; set; }
    public string? RecommendedOwnerId { get; set; }
    public string? RecommendedOwnerName { get; set; }
    public string Mode { get; set; } = "Shadow";
    public bool IsEligible { get; set; }
    public bool WasAutoApplied { get; set; }
    public double Confidence { get; set; }

    /// <summary>One of: AutoApplied, Eligible, Blocked.</summary>
    public string Result { get; set; } = "Blocked";

    public string ResultLabel { get; set; } = string.Empty;
    public string ReasonSummary { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }
}

public sealed class CortexAutonomySummaryResponse
{
    public CortexAutonomySettingsResponse Settings { get; set; } = new();
    public CortexAutonomyCountsResponse Counts { get; set; } = new();
    public List<CortexAutonomyRecentDecisionResponse> Recent { get; set; } = [];
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
}

public sealed class UpdateCortexAutonomySettingsRequest
{
    public bool? Enabled { get; set; }
    public bool? ShadowMode { get; set; }
    public double? MinConfidence { get; set; }
    public int? RecentOverrideWindowHours { get; set; }
    public bool? RequireClearWinner { get; set; }
    public double? MinAlternativeGap { get; set; }
}
