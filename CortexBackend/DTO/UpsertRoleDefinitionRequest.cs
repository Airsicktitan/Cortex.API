namespace Cortex.API.DTO;

public class UpsertRoleDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
}
