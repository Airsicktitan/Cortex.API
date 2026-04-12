using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class ArchivedTicket
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int BoardId { get; set; }
    public int? StoryPoints { get; set; }
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public int LastModifiedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int ArchivedBy { get; set; }
    public DateTime ArchivedDate { get; set; } = DateTime.UtcNow;
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }

    [JsonIgnore]
    public User? CreatedByUser { get; set; }

    [JsonIgnore]
    public User? ArchivedByUser { get; set; }

    [JsonIgnore]
    public TicketBoardDefinition? BoardDefinition { get; set; }
}
