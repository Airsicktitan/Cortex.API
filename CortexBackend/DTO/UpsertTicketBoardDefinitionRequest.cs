namespace Cortex.API.DTO;

public class UpsertTicketBoardDefinitionRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool RequiresStoryPoints { get; set; }
    public bool IsEnabled { get; set; } = true;
}
