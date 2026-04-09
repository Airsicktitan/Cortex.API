namespace Cortex.API.DTO;

public class OnlineUserResponse
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public string? NickName { get; set; }
    public required string Email { get; set; }
    public string? Department { get; set; }
    public required string Role { get; set; }
    public DateTime? LastSeenDateUtc { get; set; }
    public DateTime? LastLoginDate { get; set; }
}
