namespace Cortex.API.DTO;

public sealed class AiAssessRequest
{
    /// <summary>Load the ticket from persistence (comments, persisted vision JSON, board).</summary>
    public string? TicketId { get; set; }

    /// <summary>Optional snapshot when assessing a draft or external payload without a stored id.</summary>
    public AiAssessTicketPayload? Ticket { get; set; }
}

public sealed class AiAssessTicketPayload
{
    public string? Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "New";
    public int BoardId { get; set; }
    public string? Department { get; set; }
    public string? AiScreenshotInsightJson { get; set; }
    public List<AiAssessCommentPayload>? Comments { get; set; }
}

public sealed class AiAssessCommentPayload
{
    public string Body { get; set; } = "";
}
