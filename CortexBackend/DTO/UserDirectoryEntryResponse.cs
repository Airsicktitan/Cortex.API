namespace Cortex.API.DTO;

public class UserDirectoryEntryResponse
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public string? Department { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsSynitiOwnerEligible { get; set; }
    public bool IsBusinessOwnerEligible { get; set; }
}
