using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class ArchivedComment
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public int? OriginalCommentId { get; set; }
    public string Body { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }

    [JsonIgnore]
    public ArchivedTicket ArchivedTicket { get; set; } = null!;

    [JsonIgnore]
    public User? CreatedByUser { get; set; }
}
