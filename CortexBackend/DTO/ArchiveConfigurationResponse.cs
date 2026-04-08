namespace Cortex.API.DTO;

public class ArchiveConfigurationResponse
{
    public int ArchiveAfterDays { get; set; }
    public bool ArchiveResolvedTickets { get; set; }
    public bool ArchiveClosedTickets { get; set; }
}
