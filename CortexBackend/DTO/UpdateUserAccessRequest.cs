namespace Cortex.API.DTO;

public class UpdateUserAccessRequest
{
    public string? Role { get; set; }
    public IReadOnlyList<string>? Permissions { get; set; }
}
