using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface ITicketTriageResponseValidator
{
    TicketTriageValidatedResult Validate(
        TicketTriageGenerateResponse response,
        TicketTriageVocabularySnapshot vocabulary);
}

public sealed class TicketTriageResponseValidator : ITicketTriageResponseValidator
{
    private static readonly string[] AllowedSlaRiskTiers = ["Low", "Medium", "High"];

    public TicketTriageValidatedResult Validate(
        TicketTriageGenerateResponse response,
        TicketTriageVocabularySnapshot vocabulary)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(vocabulary);

        var errors = new List<string>();

        var summary = NormalizeText(response.Summary);
        if (summary is null)
        {
            errors.Add("summary is required.");
        }

        var priority = MatchCanonical(response.SuggestedPriority, vocabulary.Priorities.Select(x => x.Name));
        if (priority is null)
        {
            errors.Add("priority must match a configured value.");
        }

        var priorityReason = NormalizeText(response.PriorityReason);
        if (priorityReason is null)
        {
            errors.Add("priorityReason is required.");
        }

        string? status = null;
        if (vocabulary.Statuses.Count > 0)
        {
            status = MatchCanonical(
                response.SuggestedStatus,
                vocabulary.Statuses.OrderBy(x => x.SortKey).Select(x => x.Name));

            if (status is null)
            {
                errors.Add("status must match a configured value when statuses are configured.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(response.SuggestedStatus))
        {
            errors.Add("status must be omitted when no statuses are configured.");
        }

        var missingDetails = NormalizeMissing(response.MissingDetails, out var missingDetailCount);
        if (missingDetailCount < 2 || missingDetailCount > 4)
        {
            errors.Add("missingDetails must contain 2 to 4 non-empty items.");
        }

        var potentialSlaRisk = MatchCanonical(response.PotentialSlaRisk, AllowedSlaRiskTiers);
        if (potentialSlaRisk is null)
        {
            errors.Add("potentialSlaRisk must be one of: Low, Medium, High.");
        }

        var slaRiskReason = NormalizeText(response.SlaRiskReason);
        if (slaRiskReason is null)
        {
            errors.Add("slaRiskReason is required.");
        }

        return new TicketTriageValidatedResult
        {
            Summary = summary,
            Priority = priority,
            PriorityReason = priorityReason,
            Status = status,
            MissingDetails = missingDetails,
            PotentialSlaRisk = potentialSlaRisk,
            SlaRiskReason = slaRiskReason,
            IsValid = errors.Count == 0,
            ValidationErrors = errors,
        };
    }

    private static string? MatchCanonical(string? raw, IEnumerable<string> allowed)
    {
        var candidate = NormalizeText(raw);
        if (candidate is null)
        {
            return null;
        }

        foreach (var value in allowed)
        {
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static List<string> NormalizeMissing(IReadOnlyCollection<string>? items, out int normalizedCount)
    {
        var normalized = (items ?? [])
            .Select(NormalizeText)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        normalizedCount = normalized.Count;

        if (normalized.Count > 4)
        {
            normalized = normalized.Take(4).ToList();
        }

        return normalized;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TicketTriageValidatedResult
{
    public string? Summary { get; init; }
    public string? Priority { get; init; }
    public string? PriorityReason { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<string> MissingDetails { get; init; } = [];
    public string? PotentialSlaRisk { get; init; }
    public string? SlaRiskReason { get; init; }
    public bool IsValid { get; init; }
    public bool UsedFallback { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}
