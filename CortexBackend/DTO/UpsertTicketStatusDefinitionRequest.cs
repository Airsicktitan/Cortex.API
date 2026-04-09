namespace Cortex.API.DTO;

public class UpsertTicketStatusDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}
