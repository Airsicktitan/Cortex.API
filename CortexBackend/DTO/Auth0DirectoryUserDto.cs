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

    /// <summary>Auth0 root <c>nickname</c>. Default = JSON property omitted.</summary>
    [JsonPropertyName("nickname")]
    public Auth0NicknameField Nickname { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("blocked")]
    public bool Blocked { get; set; }
}
