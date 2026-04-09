namespace Cortex.API.DTO;

public class TicketStatusDefinitionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string CreatedDateUtc { get; set; } = string.Empty;
    public string? LastModifiedDateUtc { get; set; }
}
