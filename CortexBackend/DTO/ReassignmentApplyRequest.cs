namespace Cortex.API.DTO;

public sealed class ReassignmentApplyRequest
{
    public string? TicketId { get; set; }

    public int SelectedOwnerId { get; set; }

    public string? Reason { get; set; }

    public string? Source { get; set; }

    /// <summary>Optional optimistic concurrency check from current ticket payload.</summary>
    public string? ConcurrencyToken { get; set; }

    /// <summary>Optional stale-check against the owner shown during review.</summary>
    public string? ExpectedCurrentOwnerKey { get; set; }
}

public sealed class ReassignmentApplyResponse
{
    public string TicketId { get; set; } = string.Empty;

    public string PreviousOwner { get; set; } = string.Empty;

    public string NewOwner { get; set; } = string.Empty;

    public bool Applied { get; set; }

    public DateTime AppliedAtUtc { get; set; }

    public string AuditMessage { get; set; } = string.Empty;

    public string ReassignmentSource { get; set; } = "cortex_recommendation_review";

    public TicketResponse? Ticket { get; set; }
}
