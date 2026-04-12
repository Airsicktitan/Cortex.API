namespace Cortex.API.DTO;

public class ArchivedTicketResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int BoardId { get; set; }
    public string BoardName { get; set; } = string.Empty;
    public int? StoryPoints { get; set; }
    public string? SynitiOwner { get; set; }
    public string? BusinessOwner { get; set; }
    public int CreatedBy { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public int LastModifiedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int ArchivedBy { get; set; }
    public string ArchivedByDisplayName { get; set; } = string.Empty;
    public DateTime ArchivedDate { get; set; }
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }
}
