namespace Cortex.API.DTO;

public class CommentResponse
{
    public int Id { get; set; }
    public string TicketId { get; set; } = null!;
    public string Body { get; set; } = null!;
    public int CreatedBy { get; set; }
    public string CreatedByDisplayName { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
}