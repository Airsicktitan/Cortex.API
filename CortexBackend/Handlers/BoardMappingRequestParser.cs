using System.Text.Json;
using Cortex.API.DTO;

namespace Cortex.API.Handlers;

/// <summary>
/// Parses PUT bodies for board mapping replace: either a raw JSON array or <c>{"mappings":[...]}</c>.
/// </summary>
public static class BoardMappingRequestParser
{
    public static async Task<IReadOnlyList<ExternalBoardMappingItemRequest>> ParseRequestBodyAsync(
        Stream body,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return ParseRoot(document.RootElement, jsonOptions);
    }

    public static IReadOnlyList<ExternalBoardMappingItemRequest> ParseRoot(
        JsonElement root,
        JsonSerializerOptions jsonOptions)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.Deserialize<List<ExternalBoardMappingItemRequest>>(jsonOptions) ?? [];
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("mappings", out var mappingsElement)
            && mappingsElement.ValueKind == JsonValueKind.Array)
        {
            return mappingsElement.Deserialize<List<ExternalBoardMappingItemRequest>>(jsonOptions) ?? [];
        }

        throw new ArgumentException(
            "Expected a JSON array of board mappings or an object with a \"mappings\" array property.");
    }
}
