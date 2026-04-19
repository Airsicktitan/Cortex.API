namespace Cortex.API.DTO;

/// <summary>
/// Evaluates routing rules against draft ticket fields without persisting (modal live preview).
/// Requester department/role are taken from the ticket creator on the server.
/// </summary>
public sealed class RoutingPreviewRequest
{
    public string TicketId { get; set; } = string.Empty;

    public int BoardId { get; set; }

    public string Priority { get; set; } = string.Empty;

    public string? Title { get; set; }

    /// <summary>Legacy/requester department field used in routing rules (optional).</summary>
    public string? Department { get; set; }
}
