namespace Cortex.API.DTO;

public sealed class OwnerWorkloadPreviewResponse
{
    public List<OwnerWorkloadSummaryDto> Summaries { get; set; } = [];
}

public sealed class OwnerWorkloadSummaryDto
{
    public string OwnerKey { get; set; } = string.Empty;

    public int ActiveTicketCount { get; set; }

    public int HighPriorityTicketCount { get; set; }

    public int AtRiskTicketCount { get; set; }

    /// <summary>Open tickets whose SLA status is Breached.</summary>
    public int OutsideSlaOpenCount { get; set; }

    /// <summary>Open tickets currently At Risk or Breached.</summary>
    public int SlaRiskTicketCount { get; set; }

    /// <summary>Starter workload score: open + (high priority * 2) + (SLA risk * 3).</summary>
    public int WorkloadScore { get; set; }
}
