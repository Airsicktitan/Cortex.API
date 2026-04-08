namespace Cortex.API.DTO;

public class UpdateArchiveConfigurationRequest
{
    public int ArchiveAfterDays { get; set; }
    public bool ArchiveResolvedTickets { get; set; }
    public bool ArchiveClosedTickets { get; set; }
}
