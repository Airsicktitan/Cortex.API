namespace Cortex.API.DTO;

/// <summary>
/// Request for the user-facing Improve Request intake assist. Stateless: no ticket is created or updated.
/// </summary>
public sealed class IntakeAssistRequest
{
    /// <summary>Draft title as the requester typed it (may be empty or vague).</summary>
    public string? Title { get; set; }

    /// <summary>Draft description as the requester typed it (may be empty or vague).</summary>
    public string? Description { get; set; }

    /// <summary>Optional display name of the board the requester selected; used only as background context.</summary>
    public string? BoardName { get; set; }
}
