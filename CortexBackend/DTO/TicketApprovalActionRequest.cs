namespace Cortex.API.DTO;

public sealed class TicketApprovalActionRequest
{
    /// <summary>Optional for approve; used for return / reject.</summary>
    public string? Reason { get; set; }
}
