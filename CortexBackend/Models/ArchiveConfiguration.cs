namespace Cortex.API.Models;

public class ArchiveConfiguration
{
    public int Id { get; set; }
    public int ArchiveAfterDays { get; set; }
    public bool ArchiveResolvedTickets { get; set; }
    public bool ArchiveClosedTickets { get; set; }
}
