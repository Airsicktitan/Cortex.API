using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class Comment
{
    public int Id { get; set; }

    // Foreign key to the associated ticket
    public string TicketId { get; set; } = null!;

    // Navigation property to the associated ticket
    [JsonIgnore] // Prevent circular reference during JSON serialization
    public Ticket Ticket { get; set; } = null!;

    public string Body { get; set; } = null!;

    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [JsonIgnore] // Prevent circular reference during JSON serialization
    public User CreatedByUser { get; set; } = null!;

}   