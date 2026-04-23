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

    /// <summary>Open tickets currently near SLA breach.</summary>
    public int SlaRiskTicketCount { get; set; }

    /// <summary>Open tickets with no activity in the stale-work window.</summary>
    public int StaleTicketCount { get; set; }

    /// <summary>Open + high priority*2 + overdue*3 + SLA risk*2.5 + stale*1.5.</summary>
    public decimal WorkloadScore { get; set; }
}
