using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Advisory AI review of a recurring issue group. Generates an executive-friendly
/// summary explaining the recurring pattern, its operational impact, and categorized
/// next-step suggestions (e.g. root-cause fix, automation, documentation).
///
/// Governance:
/// - Respects <see cref="Models.AiSettingsConfiguration.IsTriageEnabled"/> as the advisory
///   text-AI umbrella until a dedicated flag is introduced.
/// - Advisory-only: never performs irreversible actions. Returns <see cref="RepeatIssueAiReviewResponse.Unavailable"/>
///   when AI is not configured, disabled, or errored.
/// </summary>
public interface IRepeatIssueAiReviewService
{
    Task<RepeatIssueAiReviewResponse> GenerateReviewAsync(
        RepeatIssueAiReviewInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>Input to the AI review — a compact serialisable snapshot of the group, no navigation entities.</summary>
public sealed class RepeatIssueAiReviewInput
{
    public required string GroupKey { get; init; }
    public required string RepresentativeTitle { get; init; }
    public required string BoardName { get; init; }
    public required List<string> SignatureTokens { get; init; }
    public required int RepeatCount { get; init; }
    public required int OpenCount { get; init; }
    public required DateTime FirstSeenUtc { get; init; }
    public required DateTime LastSeenUtc { get; init; }
    public double? AvgResolutionHours { get; init; }
    public required double TotalResolutionHours { get; init; }
    public required int OperationalTouchCount { get; init; }
    public required int TrendDelta { get; init; }
    public required string TrendLabel { get; init; }
    public string? DominantPriority { get; init; }
    public string? DominantStatus { get; init; }

    /// <summary>Up to N concise ticket samples used to ground the summary.</summary>
    public required List<RepeatIssueAiReviewSampleTicket> SampleTickets { get; init; }
}

public sealed class RepeatIssueAiReviewSampleTicket
{
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
    public required DateTime CreatedDate { get; init; }
    public double? ResolutionHours { get; init; }
    public int CommentCount { get; init; }
}
