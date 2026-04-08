namespace Cortex.API.DTO;

public class TicketAttachmentResponse
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int UploadedBy { get; set; }
    public string UploadedByDisplayName { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
}
