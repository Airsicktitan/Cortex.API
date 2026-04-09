using Cortex.API.Models;

namespace Cortex.API.DTO;

public sealed class ResponseMappingContext(
    IReadOnlyDictionary<int, string> userDisplayNames,
    IReadOnlyDictionary<int, string> storedProcedureLabels)
{
    public static ResponseMappingContext Empty { get; } = new(
        new Dictionary<int, string>(),
        new Dictionary<int, string>());

    public string ResolveUserDisplayName(int userId, User? loadedUser = null)
    {
        var loadedDisplayName = NormalizeDisplayName(loadedUser?.DisplayName)
            ?? NormalizeDisplayName(loadedUser?.Email);

        if (loadedDisplayName is not null)
        {
            return loadedDisplayName;
        }

        return userDisplayNames.TryGetValue(userId, out var displayName)
            ? displayName
            : "Unknown User";
    }

    public string? ResolveStoredProcedureLabel(
        int? storedProcedureDefinitionId,
        StoredProcedureDefinition? loadedDefinition = null)
    {
        var loadedLabel = NormalizeDisplayName(loadedDefinition?.Name);
        if (loadedLabel is not null)
        {
            return loadedLabel;
        }

        if (storedProcedureDefinitionId.HasValue
            && storedProcedureLabels.TryGetValue(storedProcedureDefinitionId.Value, out var label))
        {
            return label;
        }

        return null;
    }

    private static string? NormalizeDisplayName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
