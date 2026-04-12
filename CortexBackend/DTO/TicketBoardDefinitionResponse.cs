namespace Cortex.API.DTO;

public class TicketBoardDefinitionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresStoryPoints { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
}
