using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class ArchivedTicketAttachment
{
    public int Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public int? OriginalAttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public byte[] Content { get; set; } = [];
    public int UploadedBy { get; set; }
    public DateTime UploadedDate { get; set; }

    [JsonIgnore]
    public ArchivedTicket ArchivedTicket { get; set; } = null!;

    [JsonIgnore]
    public User? UploadedByUser { get; set; }
}
