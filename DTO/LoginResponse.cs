namespace Cortex.API.DTO;

public class LoginResponse
{
    public required string Token { get; set; }
    public required DateTime Expiration { get; set; }

    public required string Username { get; set; }
    public required string Role { get; set; }
}