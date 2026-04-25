using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IRebalanceAiAdvisoryService
{
    /// <summary>
    /// Adds concise advisory language after deterministic rebalance selection.
    /// The returned text may explain decisions, but never changes ownership.
    /// </summary>
    Task<IReadOnlyDictionary<string, RebalanceAiAdvisory>> GenerateAdvisoriesAsync(
        IReadOnlyList<RebalanceAiDecisionPacket> packets,
        CancellationToken cancellationToken = default);
}

public sealed class RebalanceAiDecisionPacket
{
    public string TicketId { get; set; } = string.Empty;
    public string TicketTitle { get; set; } = string.Empty;
    public string TicketSummary { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> TicketSignals { get; set; } = [];
    public RebalanceAiOwnerSnapshot CurrentOwner { get; set; } = new();
    public RebalanceAiOwnerSnapshot SelectedOwner { get; set; } = new();
    public string RawTopCandidateName { get; set; } = string.Empty;
    public string FinalCandidateName { get; set; } = string.Empty;
    public bool DiversificationApplied { get; set; }
    public List<string> DeterministicReasons { get; set; } = [];
    public List<RebalanceAiCandidateOption> CandidateOptions { get; set; } = [];
}

public sealed class RebalanceAiOwnerSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ActiveTicketCount { get; set; }
    public int SlaRiskCount { get; set; }
    public int HighPriorityCount { get; set; }
    public int StaleTicketCount { get; set; }
    public decimal WorkloadScore { get; set; }
    public decimal ProjectedWorkloadScore { get; set; }
    public int IncomingRecommendationCount { get; set; }
}

public sealed class RebalanceAiCandidateOption
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal WorkloadScore { get; set; }
    public decimal ProjectedWorkloadScore { get; set; }
    public string PressureLevel { get; set; } = "low";
    public int RankBeforeDiversification { get; set; }
    public int RankAfterDiversification { get; set; }
    public string Outcome { get; set; } = string.Empty;
}
