namespace Cortex.API.DTO;

/// <summary>
/// Read-only intake learning aggregates (follow-up friction patterns; correlation, not causation).
/// </summary>
public sealed class IntakeLearningOverviewDto
{
    public List<IntakeLearningGroupDto> BoardReturns { get; init; } = [];

    public List<IntakeLearningGroupDto> PriorityReturns { get; init; } = [];

    public List<IntakeLearningGroupDto> DepartmentReturns { get; init; } = [];

    /// <summary>
    /// Count of cohort tickets whose creator has no department (null/empty/whitespace).
    /// </summary>
    public int UnknownDepartmentTicketCount { get; init; }

    public ReturnReasonAvailabilityDto ReturnReasonAvailability { get; init; } = new();

    public MissingHintSummaryDto MissingHintSummary { get; init; } = new();

    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>Buyer-facing data caveats for this MVP.</summary>
    public IReadOnlyList<string> Limitations { get; init; } = [];
}

public sealed class IntakeLearningGroupDto
{
    /// <summary>
    /// Stable grouping key (<c>board-{id}</c>, priority string, or department slug / <c>unknown</c>).
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int TotalTickets { get; init; }

    public int ReturnedTickets { get; init; }

    /// <summary>0–100 with two decimal places where applicable; 0 when denominator is 0.</summary>
    public double ReturnRatePercent { get; init; }
}

public sealed class ReturnReasonAvailabilityDto
{
    /// <summary>Tickets with <see cref="Models.TicketOutcome.WasReturnedForDetail"/>.</summary>
    public int ReturnedTickets { get; init; }

    public int ReturnReasonStillAvailableCount { get; init; }

    /// <summary>0–100; 0 when <see cref="ReturnedTickets"/> is 0.</summary>
    public double ReturnReasonAvailabilityPercent { get; init; }
}

public sealed class MissingHintSummaryDto
{
    public int ReturnedTickets { get; init; }

    public int ReturnedTicketsWithMissingHintJson { get; init; }

    public double AverageMissingHintCount { get; init; }

    public int ZeroHintsCount { get; init; }

    public int OneToTwoHintsCount { get; init; }

    public int ThreeToFiveHintsCount { get; init; }

    public int SixPlusHintsCount { get; init; }
}
