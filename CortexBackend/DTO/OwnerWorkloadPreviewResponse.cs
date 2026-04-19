namespace Cortex.API.DTO;

public sealed class OwnerWorkloadPreviewResponse
{
    public List<OwnerWorkloadSummaryDto> Summaries { get; set; } = [];
}

public sealed class OwnerWorkloadSummaryDto
{
    public string OwnerKey { get; set; } = string.Empty;

    public int ActiveTicketCount { get; set; }

    public int AtRiskTicketCount { get; set; }

    /// <summary>Open tickets whose SLA status is Breached.</summary>
    public int OutsideSlaOpenCount { get; set; }
}
