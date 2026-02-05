namespace Cortex.API.Models;

public class Comment
{
    public int Id { get; set; }

    // Foreign key to the associated ticket
    public string TicketId { get; set; } = null!;

    // Navigation property to the associated ticket
    public Ticket Ticket { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string CreatedBy { get; set; } = null!; 
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

}   