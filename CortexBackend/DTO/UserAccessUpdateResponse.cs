namespace Cortex.API.DTO;

public class UserAccessUpdateResponse
{
    public required int UserId { get; set; }
    public required string Role { get; set; }
    public required IReadOnlyList<string> RequestedPermissions { get; set; }
    public required bool SyncQueued { get; set; }
}
