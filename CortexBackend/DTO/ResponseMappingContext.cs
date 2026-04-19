using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.DTO;

public sealed record ResponseMappingUserIdentity(
    string DisplayName,
    string? Email,
    string? Auth0Id);

public sealed class ResponseMappingContext(
    IReadOnlyDictionary<int, ResponseMappingUserIdentity> userIdentities,
    IReadOnlyDictionary<int, string> storedProcedureLabels,
    IReadOnlyDictionary<int, string> boardNames,
    IReadOnlyDictionary<string, User> ownerAliases)
{
    public static ResponseMappingContext Empty { get; } = new(
        new Dictionary<int, ResponseMappingUserIdentity>(),
        new Dictionary<int, string>(),
        new Dictionary<int, string>(),
        new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase));

    public string ResolveUserDisplayName(int userId, User? loadedUser = null)
    {
        var loadedDisplayName = NormalizeDisplayName(loadedUser?.DisplayName)
            ?? NormalizeDisplayName(loadedUser?.Email);

        if (loadedDisplayName is not null)
        {
            return loadedDisplayName;
        }

        return userIdentities.TryGetValue(userId, out var identity)
            ? identity.DisplayName
            : "Unknown User";
    }

    public string? ResolveUserEmail(int userId, User? loadedUser = null)
    {
        var loadedEmail = NormalizeDisplayName(loadedUser?.Email);
        if (loadedEmail is not null)
        {
            return loadedEmail;
        }

        return userIdentities.TryGetValue(userId, out var identity)
            ? NormalizeDisplayName(identity.Email)
            : null;
    }

    public string? ResolveUserAuth0Id(int userId, User? loadedUser = null)
    {
        var loadedAuth0Id = NormalizeDisplayName(loadedUser?.Auth0Id);
        if (loadedAuth0Id is not null)
        {
            return loadedAuth0Id;
        }

        return userIdentities.TryGetValue(userId, out var identity)
            ? NormalizeDisplayName(identity.Auth0Id)
            : null;
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

    public string ResolveBoardName(
        int boardId,
        TicketBoardDefinition? loadedDefinition = null)
    {
        var loadedName = NormalizeDisplayName(loadedDefinition?.Name);
        if (loadedName is not null)
        {
            return loadedName;
        }

        return boardNames.TryGetValue(boardId, out var boardName)
            ? boardName
            : "Regular";
    }

    /// <summary>
    /// Resolves a stored Syniti/Business owner token (email, <c>user:id</c>, or legacy display name) to a display label.
    /// </summary>
    public string? ResolveOwnerFieldDisplayName(string? rawStored)
    {
        if (string.IsNullOrWhiteSpace(rawStored))
        {
            return null;
        }

        var resolved = OwnerFieldResolution.ResolveUser(rawStored, ownerAliases);
        return OwnerFieldResolution.FormatOwnerDisplayForApi(rawStored, resolved);
    }

    private static string? NormalizeDisplayName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
