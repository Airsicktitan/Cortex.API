using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cortex.API.DTO;

/// <summary>
/// Auth0 root <c>nickname</c> for directory import. Default (<see cref="IsSpecified"/> <see langword="false"/>)
/// means the JSON member was omitted. <see cref="IsSpecified"/> <see langword="true"/> means Auth0 sent
/// the field (string, empty, or JSON null).
/// </summary>
[JsonConverter(typeof(Auth0NicknameFieldConverter))]
public readonly record struct Auth0NicknameField(bool IsSpecified, string? NormalizedValue)
{
    public static Auth0NicknameField Omitted => default;
}

internal sealed class Auth0NicknameFieldConverter : JsonConverter<Auth0NicknameField>
{
    public override Auth0NicknameField Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return new Auth0NicknameField(IsSpecified: true, NormalizedValue: null);
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                return new Auth0NicknameField(
                    IsSpecified: true,
                    NormalizedValue: string.IsNullOrWhiteSpace(s) ? null : s.Trim());
            }
            default:
                throw new JsonException($"Unexpected JSON token for nickname: {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Auth0NicknameField value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}
