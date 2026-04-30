namespace Cortex.API.Models;

public class ExternalBoardMapping
{
    public int Id { get; set; }
    public int ExternalWorkSourceId { get; set; }
    public int BoardId { get; set; }
    public ExternalBoardMappingMode MappingMode { get; set; } = ExternalBoardMappingMode.ReferenceOnly;
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ExternalWorkSource ExternalWorkSource { get; set; } = null!;
    public TicketBoardDefinition Board { get; set; } = null!;
}
