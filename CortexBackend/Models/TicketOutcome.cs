namespace Cortex.API.Models;

/// <summary>
/// Persisted outcome of a ticket lifecycle for advisory learning signals.
/// Captured at initial assignment, override, and terminal status.
/// Never used to mutate routing decisions — evidence only.
/// </summary>
public class TicketOutcome
{
    public int Id { get; set; }

    public string TicketId { get; set; } = string.Empty;
    public int BoardId { get; set; }

    public string? AssignedSynitiOwner { get; set; }
    public string? AssignedBusinessOwner { get; set; }

    public string? FinalSynitiOwner { get; set; }
    public string? FinalBusinessOwner { get; set; }

    public bool WasOverridden { get; set; }
    public bool SlaBreached { get; set; }
    public bool WasReassigned { get; set; }
    public bool WasReopened { get; set; }

    public int CommentCount { get; set; }

    public bool ReachedTerminalStatus { get; set; }

    public int? MatchedRuleId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
