namespace Cortex.API.DTO;

/// <summary>Explicit reviewer selection of which persisted AI triage suggestions to apply.</summary>
public sealed class TicketTriageApplyRequest
{
    public bool ApplyPriority { get; set; }

    public bool ApplyStatus { get; set; }

    /// <summary>Optional reviewer note captured in ticket audit history.</summary>
    public string? ChangeReason { get; set; }
}
