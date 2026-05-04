using System.ComponentModel.DataAnnotations.Schema;

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
    public bool WasReturnedForDetail { get; set; }
    public bool WasReassigned { get; set; }
    public bool WasReopened { get; set; }

    public int CommentCount { get; set; }

    public bool ReachedTerminalStatus { get; set; }

    public int? MatchedRuleId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    [NotMapped]
    public bool WasRoutingOverridden
    {
        get => WasOverridden;
        set => WasOverridden = value;
    }

    [NotMapped]
    public bool WasSlaBreached
    {
        get => SlaBreached;
        set => SlaBreached = value;
    }

    [NotMapped]
    public string? FinalOwner
    {
        get => FinalSynitiOwner;
        set => FinalSynitiOwner = value;
    }

    [NotMapped]
    public DateTime RecordedAt
    {
        get => CreatedAtUtc;
        set => CreatedAtUtc = value;
    }

    [NotMapped]
    public DateTime? LastUpdatedAt
    {
        get => LastUpdatedAtUtc;
        set => LastUpdatedAtUtc = value;
    }

    [NotMapped]
    public DateTime? CompletedAt
    {
        get => CompletedAtUtc;
        set => CompletedAtUtc = value;
    }
}
