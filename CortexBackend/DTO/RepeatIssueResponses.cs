namespace Cortex.API.DTO;

/// <summary>
/// Top-level Recurring Issue Intelligence overview for the Reports page.
/// Includes both aggregate headline numbers and the top recurring issue groups.
/// </summary>
public sealed class RepeatIssueOverviewResponse
{
    /// <summary>Total number of detected recurring issue groups (count &gt;= <see cref="MinimumGroupSize"/>).</summary>
    public int TotalRecurringGroups { get; set; }

    /// <summary>Sum of tickets that fall into any recurring group (not unique issues, but repeat volume).</summary>
    public int TicketsInRecurringGroups { get; set; }

    /// <summary>Tickets in recurring groups that are still open (not in a terminal status).</summary>
    public int OpenTicketsInRecurringGroups { get; set; }

    /// <summary>
    /// Total resolution time across tickets in recurring groups (sum of close-time minus open-time, in hours).
    /// Proxy for repeated operational effort — not the same as human work time.
    /// </summary>
    public double TotalResolutionHoursInRecurringGroups { get; set; }

    /// <summary>Minimum repeat count used to qualify a group as recurring for this snapshot.</summary>
    public int MinimumGroupSize { get; set; }

    /// <summary>Timestamp the snapshot was generated (UTC).</summary>
    public DateTime GeneratedUtc { get; set; }

    /// <summary>Ranked list of recurring issue groups (top N by repeat count, then recency).</summary>
    public List<RepeatIssueGroupSummary> Groups { get; set; } = [];
}

/// <summary>Ranked-list row describing one recurring issue group.</summary>
public sealed class RepeatIssueGroupSummary
{
    /// <summary>Stable URL-safe identifier composed from board + keyword signature.</summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>Human-readable label derived from the most recent ticket in the group.</summary>
    public string RepresentativeTitle { get; set; } = string.Empty;

    /// <summary>Keyword signature tokens (explains why these tickets were grouped).</summary>
    public List<string> SignatureTokens { get; set; } = [];

    /// <summary>Board the recurring pattern lives on.</summary>
    public int BoardId { get; set; }

    public string BoardName { get; set; } = string.Empty;

    /// <summary>Total tickets that match the group signature (open + closed + archived).</summary>
    public int RepeatCount { get; set; }

    /// <summary>Subset of <see cref="RepeatCount"/> that are still open (not in a terminal status).</summary>
    public int OpenCount { get; set; }

    /// <summary>Earliest CreatedDate across tickets in the group.</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>Most recent CreatedDate across tickets in the group.</summary>
    public DateTime LastSeenUtc { get; set; }

    /// <summary>
    /// Average resolution time across closed/archived tickets in hours.
    /// Based on LastModifiedDate (or ArchivedDate) minus CreatedDate — proxy, not human work time.
    /// Null when the group has no closed tickets.
    /// </summary>
    public double? AvgResolutionHours { get; set; }

    /// <summary>
    /// Total resolution time across closed/archived tickets in hours (sum of close - open).
    /// Proxy for repeated operational effort on this recurring issue.
    /// </summary>
    public double TotalResolutionHours { get; set; }

    /// <summary>Sum of comments across tickets in the group (operational touch count).</summary>
    public int OperationalTouchCount { get; set; }

    /// <summary>
    /// Trend: positive = more occurrences in the last 30 days than in the prior 30,
    /// negative = fewer. Returns 0 when volumes match or history is too short.
    /// </summary>
    public int TrendDelta { get; set; }

    /// <summary>Human label for the trend: "rising", "falling", or "stable".</summary>
    public string TrendLabel { get; set; } = "stable";
}

/// <summary>Detail for a single selected recurring issue group.</summary>
public sealed class RepeatIssueGroupDetailResponse
{
    public RepeatIssueGroupSummary Summary { get; set; } = new();

    /// <summary>Distinct board names touched by tickets in the group (usually 1 — groups are board-scoped).</summary>
    public List<string> Boards { get; set; } = [];

    /// <summary>Distinct owner labels (Syniti + Business) covering tickets in the group.</summary>
    public List<string> Owners { get; set; } = [];

    /// <summary>Most common priority across tickets in the group.</summary>
    public string? DominantPriority { get; set; }

    /// <summary>Most common current status across tickets in the group.</summary>
    public string? DominantStatus { get; set; }

    /// <summary>Compact list of the tickets that match this recurring group.</summary>
    public List<RepeatIssueTicketSummary> Tickets { get; set; } = [];
}

/// <summary>One row per ticket within a recurring group detail view.</summary>
public sealed class RepeatIssueTicketSummary
{
    public string TicketId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public double? ResolutionHours { get; set; }
    public int CommentCount { get; set; }
    public string? Owner { get; set; }
}

/// <summary>
/// Advisory AI review of one recurring issue group (summary, impact, next-step categories).
/// Returns <see cref="Unavailable"/> when AI is not configured, disabled, or errored.
/// </summary>
public sealed class RepeatIssueAiReviewResponse
{
    public string? Summary { get; set; }

    /// <summary>One sentence describing the operational impact.</summary>
    public string? Impact { get; set; }

    /// <summary>"Rising", "Falling", or "Stable" — based on the supplied trend + signals.</summary>
    public string? TrendCommentary { get; set; }

    /// <summary>2–5 short bullets naming patterns or shared characteristics.</summary>
    public List<string> CommonCharacteristics { get; set; } = [];

    /// <summary>2–4 suggested next-step categories (e.g. Root-cause fix, Automation, Documentation, Training, Monitoring).</summary>
    public List<RepeatIssueSuggestedStep> SuggestedNextSteps { get; set; } = [];

    public bool Unavailable { get; set; }
    public string? UnavailableReason { get; set; }
}

public sealed class RepeatIssueSuggestedStep
{
    public string Category { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
}
