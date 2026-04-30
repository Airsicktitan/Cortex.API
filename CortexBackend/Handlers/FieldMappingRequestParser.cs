using System.Text.Json;
using Cortex.API.DTO;

namespace Cortex.API.Handlers;

/// <summary>
/// Parses PUT bodies for field mapping replace: either a raw JSON array or <c>{"mappings":[...]}</c>.
/// </summary>
public static class FieldMappingRequestParser
{
    public static async Task<IReadOnlyList<ExternalFieldMappingItemRequest>> ParseRequestBodyAsync(
        Stream body,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return ParseRoot(document.RootElement, jsonOptions);
    }

    public static IReadOnlyList<ExternalFieldMappingItemRequest> ParseRoot(
        JsonElement root,
        JsonSerializerOptions jsonOptions)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.Deserialize<List<ExternalFieldMappingItemRequest>>(jsonOptions) ?? [];
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("mappings", out var mappingsElement)
            && mappingsElement.ValueKind == JsonValueKind.Array)
        {
            return mappingsElement.Deserialize<List<ExternalFieldMappingItemRequest>>(jsonOptions) ?? [];
        }

        throw new ArgumentException(
            "Expected a JSON array of field mappings or an object with a \"mappings\" array property.");
    }
}
