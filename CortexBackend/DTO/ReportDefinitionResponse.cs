namespace Cortex.API.DTO;

public class ReportDefinitionResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string SqlQuery { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
}
