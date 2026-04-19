using System.Text.Json.Serialization;

namespace Cortex.API.DTO;

/// <summary>Subset of Auth0 GET /api/v2/users fields used for directory sync.</summary>
public sealed class Auth0DirectoryUserDto
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("blocked")]
    public bool Blocked { get; set; }
}
