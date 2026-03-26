namespace Cortex.API.DTO;

public class UpdateUserProfileRequest
{
    public required string DisplayName { get; set; }
    public string? Department { get; set; }
}