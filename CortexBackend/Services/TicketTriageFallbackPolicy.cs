namespace Cortex.API.Services;

public interface ITicketTriageFallbackPolicy
{
    TicketTriageValidatedResult Apply(
        TicketTriageValidatedResult validationResult,
        TicketTriageVocabularySnapshot vocabulary);
}

public sealed class TicketTriageFallbackPolicy : ITicketTriageFallbackPolicy
{
    private const string DefaultSummary = "Clarify the requested outcome and approval needs.";
    private const string DefaultPriorityReason = "Default priority applied — reviewer assessment required.";
    private const string DefaultSlaRisk = "Medium";
    private const string DefaultSlaRiskReason =
        "Clarification needed to assess delivery pressure.";

    private static readonly string[] DefaultMissingDetails =
    [
        "Confirm the exact business outcome required.",
        "Identify the owner, approver, or team needed for next action.",
    ];

    public TicketTriageValidatedResult Apply(
        TicketTriageValidatedResult validationResult,
        TicketTriageVocabularySnapshot vocabulary)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        ArgumentNullException.ThrowIfNull(vocabulary);

        var priority = validationResult.Priority ?? vocabulary.Priorities.FirstOrDefault()?.Name;
        var status = vocabulary.Statuses.Count > 0
            ? validationResult.Status ?? vocabulary.Statuses.OrderBy(x => x.SortKey).Select(x => x.Name).FirstOrDefault()
            : null;
        var missingDetails = BuildMissingDetails(validationResult.MissingDetails);
        var summary = validationResult.Summary ?? DefaultSummary;
        var priorityReason = validationResult.PriorityReason ?? DefaultPriorityReason;
        var potentialSlaRisk = validationResult.PotentialSlaRisk ?? DefaultSlaRisk;
        var slaRiskReason = validationResult.SlaRiskReason ?? DefaultSlaRiskReason;

        return new TicketTriageValidatedResult
        {
            Summary = summary,
            Priority = priority,
            PriorityReason = priorityReason,
            Status = status,
            MissingDetails = missingDetails,
            PotentialSlaRisk = potentialSlaRisk,
            SlaRiskReason = slaRiskReason,
            UsedFallback = true,
            IsValid =
                !string.IsNullOrWhiteSpace(summary)
                && !string.IsNullOrWhiteSpace(priority)
                && !string.IsNullOrWhiteSpace(priorityReason)
                && (vocabulary.Statuses.Count == 0 || !string.IsNullOrWhiteSpace(status))
                && missingDetails.Count >= 2
                && missingDetails.Count <= 4
                && !string.IsNullOrWhiteSpace(potentialSlaRisk)
                && !string.IsNullOrWhiteSpace(slaRiskReason),
            ValidationErrors = validationResult.ValidationErrors.ToArray(),
        };
    }

    private static List<string> BuildMissingDetails(IReadOnlyList<string> candidateMissingDetails)
    {
        if (candidateMissingDetails.Count >= 2 && candidateMissingDetails.Count <= 4)
        {
            return candidateMissingDetails.ToList();
        }

        return [.. DefaultMissingDetails];
    }
}
