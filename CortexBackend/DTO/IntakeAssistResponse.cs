namespace Cortex.API.DTO;

/// <summary>
/// Allowed clarity states for the Improve Request flow.
/// The server always maps AI output onto one of these canonical values so the client can
/// switch UI without pattern-matching on free text.
/// </summary>
public static class IntakeAssistClarityStates
{
    public const string ReadyForExecution = "ready_for_execution";
    public const string RequiresClarification = "requires_clarification";
    public const string WouldHaveRequiredFollowUp = "would_have_required_follow_up";
}

/// <summary>
/// Response for the user-facing Improve Request intake assist. Nothing in this payload is persisted
/// server-side; the requester chooses whether to adopt any of it before submitting the ticket.
/// </summary>
public sealed class IntakeAssistResponse
{
    /// <summary>One-sentence rewrite of the title the requester can adopt verbatim.</summary>
    public string? SuggestedSummary { get; set; }

    /// <summary>Improved description preserving the requester's intent; never invents new facts.</summary>
    public string? ImprovedDescription { get; set; }

    /// <summary>Short, actionable details the requester should add (empty when the request is already clear).</summary>
    public List<string> MissingDetails { get; set; } = [];

    /// <summary>
    /// One of <see cref="IntakeAssistClarityStates"/>. The client drives the pill/guidance off this value only.
    /// </summary>
    public string ClarityState { get; set; } = IntakeAssistClarityStates.RequiresClarification;

    /// <summary>Single-sentence coaching message aligned with the clarity state.</summary>
    public string? GuidanceMessage { get; set; }

    /// <summary>True when the provider is not configured or generation was skipped; nothing else is populated.</summary>
    public bool Unavailable { get; set; }

    /// <summary>User-safe reason explaining why assist is unavailable (only set when Unavailable is true).</summary>
    public string? UnavailableReason { get; set; }
}
