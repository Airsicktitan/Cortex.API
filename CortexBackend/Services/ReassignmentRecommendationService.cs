using System.Text.Json;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed class ReassignmentRecommendationService(
    ITicketRoutingRuleService ticketRoutingRuleService,
    IOwnerWorkloadScoringService ownerWorkloadScoringService,
    IOperationalRiskService operationalRiskService,
    IUserRepository userRepository) : IReassignmentRecommendationService
{
    private const int MaxTargets = 3;
    private const decimal MeaningfulWorkloadDelta = 3m;

    private readonly ITicketRoutingRuleService _ticketRoutingRuleService = ticketRoutingRuleService;
    private readonly IOwnerWorkloadScoringService _ownerWorkloadScoringService = ownerWorkloadScoringService;
    private readonly IOperationalRiskService _operationalRiskService = operationalRiskService;
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<ReassignmentRecommendationResponse> EvaluateAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        var batch = await EvaluateBatchAsync([ticket], cancellationToken);
        return batch.TryGetValue(ticket.Id, out var recommendation)
            ? recommendation
            : BuildNoSuggestion("Reassignment recommendation unavailable.", "unassigned");
    }

    public async Task<IReadOnlyDictionary<string, ReassignmentRecommendationResponse>> EvaluateBatchAsync(
        IEnumerable<Ticket> tickets,
        CancellationToken cancellationToken = default)
    {
        var list = tickets.ToList();
        if (list.Count == 0)
        {
            return new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal);
        }

        var users = (await _userRepository.GetAllUsersAsync()).ToList();
        var userMatchLookup = BuildUserMatchLookup(users);
        var riskByTicket = await _operationalRiskService.EvaluateBatchAsync(list, cancellationToken);

        var output = new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal);

        foreach (var ticket in list)
        {
            output[ticket.Id] = await EvaluateInternalAsync(
                ticket,
                riskByTicket.TryGetValue(ticket.Id, out var risk) ? risk : null,
                users,
                userMatchLookup,
                cancellationToken);
        }

        return output;
    }

    private async Task<ReassignmentRecommendationResponse> EvaluateInternalAsync(
        Ticket ticket,
        OperationalRiskResponse? risk,
        IReadOnlyList<User> users,
        IReadOnlyDictionary<string, User> userMatchLookup,
        CancellationToken cancellationToken)
    {
        var assignmentField = ResolveAssignmentField(ticket);
        if (assignmentField == "unassigned")
        {
            return BuildNoSuggestion("Current owner is missing.", assignmentField);
        }

        var shouldEvaluate =
            risk is not null &&
            (risk.RiskLevel is "high" or "critical" || risk.IsOwnerOverloaded);
        if (!shouldEvaluate)
        {
            return BuildNoSuggestion(
                "Current assignment is not elevated enough for reassignment suggestions.",
                assignmentField);
        }

        var requester = await _userRepository.GetByIdAsync(ticket.CreatedBy);
        var factors = new RoutingFactors(
            BoardId: ticket.BoardId.ToString(),
            Priority: Normalize(ticket.Priority),
            RequesterDepartment: Normalize(requester?.Department),
            RequesterRole: Normalize(requester?.Role),
            LegacyDepartment: Normalize(requester?.Department),
            LegacyTitle: Normalize(ticket.Title));
        var routingDecision = await _ticketRoutingRuleService.EvaluateAsync(
            factors,
            ticket.Id,
            cancellationToken);
        var candidateAssignments = ParseCandidateAssignments(routingDecision.ExplanationJson);
        if (candidateAssignments.Count == 0)
        {
            return BuildNoSuggestion("No eligible reassignment alternatives are available.", assignmentField);
        }

        var currentOwnerKey = assignmentField == "synitiOwner"
            ? Normalize(ticket.SynitiOwner)
            : Normalize(ticket.BusinessOwner);
        if (currentOwnerKey.Length == 0)
        {
            return BuildNoSuggestion("Current owner is missing.", assignmentField);
        }
        currentOwnerKey = CanonicalizeOwnerKey(currentOwnerKey, userMatchLookup);

        var eligibleOwnerKeys = candidateAssignments
            .Select(candidate => assignmentField == "synitiOwner" ? candidate.SynitiOwner : candidate.BusinessOwner)
            .Where(ownerKey => ownerKey.Length > 0)
            .Select(ownerKey => CanonicalizeOwnerKey(ownerKey, userMatchLookup))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        AddDepartmentDeveloperPool(
            eligibleOwnerKeys,
            users,
            requester,
            routingDecision,
            assignmentField);

        if (eligibleOwnerKeys.Count == 0)
        {
            return BuildNoSuggestion("No eligible reassignment alternatives are available.", assignmentField);
        }

        if (!eligibleOwnerKeys.Contains(currentOwnerKey, StringComparer.OrdinalIgnoreCase))
        {
            eligibleOwnerKeys.Add(currentOwnerKey);
        }

        var ownerScores = await _ownerWorkloadScoringService.GetScoresAsync(
            eligibleOwnerKeys,
            excludeTicketId: ticket.Id,
            respectCurrentVisibility: true,
            cancellationToken);
        var ownerScoreLookup = ownerScores.ToDictionary(score => score.OwnerKey, StringComparer.OrdinalIgnoreCase);
        var currentWorkloadScore = ownerScoreLookup.TryGetValue(currentOwnerKey, out var currentScore)
            ? currentScore.WorkloadScore
            : 0;
        var currentPressure = ToPressureLevel(currentWorkloadScore);
        var currentSnapshot = BuildCurrentOwnerSnapshot(
            currentOwnerKey,
            currentWorkloadScore,
            currentPressure,
            userMatchLookup);

        var betterTargets = eligibleOwnerKeys
            .Where(ownerKey => !string.Equals(ownerKey, currentOwnerKey, StringComparison.OrdinalIgnoreCase))
            .Select(ownerKey => BuildTarget(
                ownerKey,
                ownerScoreLookup.TryGetValue(ownerKey, out var score) ? score.WorkloadScore : 0,
                userMatchLookup,
                currentWorkloadScore,
                currentPressure))
            .Where(target => target.IsBetterThanCurrent)
            .OrderBy(target => target.WorkloadScore)
            .ThenBy(target => PressureRank(target.PressureLevel))
            .ThenBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxTargets)
            .ToList();

        if (betterTargets.Count == 0)
        {
            return new ReassignmentRecommendationResponse
            {
                ShouldSuggestReassignment = false,
                Reason = "No eligible owners are meaningfully lower risk than the current assignment.",
                AssignmentField = assignmentField,
                CurrentOwner = currentSnapshot,
                SuggestedTargets = [],
            };
        }

        return new ReassignmentRecommendationResponse
        {
            ShouldSuggestReassignment = true,
            Reason = "Current owner has elevated workload and lower-risk eligible alternatives exist.",
            AssignmentField = assignmentField,
            CurrentOwner = currentSnapshot,
            SuggestedTargets = betterTargets,
        };
    }

    private static IReadOnlyList<CandidateAssignment> ParseCandidateAssignments(string explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(explanationJson);
            if (!document.RootElement.TryGetProperty("candidateAssignments", out var assignments)
                || assignments.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var output = new List<CandidateAssignment>();
            foreach (var candidate in assignments.EnumerateArray())
            {
                var synitiOwner = candidate.TryGetProperty("synitiOwner", out var synitiNode)
                    && synitiNode.ValueKind == JsonValueKind.String
                    ? synitiNode.GetString()
                    : null;
                var businessOwner = candidate.TryGetProperty("businessOwner", out var businessNode)
                    && businessNode.ValueKind == JsonValueKind.String
                    ? businessNode.GetString()
                    : null;

                var owners = CollectOwnerKeys(synitiOwner, businessOwner).ToList();
                if (owners.Count == 0)
                {
                    continue;
                }

                output.Add(new CandidateAssignment(
                    SynitiOwner: Normalize(synitiOwner),
                    BusinessOwner: Normalize(businessOwner)));
            }

            return output;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ReassignmentOwnerSnapshotResponse BuildCurrentOwnerSnapshot(
        string ownerKey,
        decimal workloadScore,
        string pressureLevel,
        IReadOnlyDictionary<string, User> userMatchLookup)
    {
        var normalizedKey = NormalizeForLookup(ownerKey);
        int? userId = null;
        if (normalizedKey.Length > 0 && userMatchLookup.TryGetValue(normalizedKey, out var user))
        {
            userId = user.Id;
        }

        return new ReassignmentOwnerSnapshotResponse
        {
            UserId = userId,
            OwnerKey = ownerKey,
            DisplayName = ResolveDisplayName(ownerKey, userMatchLookup),
            WorkloadScore = workloadScore,
            PressureLevel = pressureLevel,
        };
    }

    private static ReassignmentTargetResponse BuildTarget(
        string ownerKey,
        decimal workloadScore,
        IReadOnlyDictionary<string, User> userMatchLookup,
        decimal currentWorkloadScore,
        string currentPressureLevel)
    {
        var pressureLevel = ToPressureLevel(workloadScore);
        int? userId = null;
        var lookup = NormalizeForLookup(ownerKey);
        if (lookup.Length > 0 && userMatchLookup.TryGetValue(lookup, out var user))
        {
            userId = user.Id;
        }

        var betterByWorkload = (currentWorkloadScore - workloadScore) >= MeaningfulWorkloadDelta;
        var betterByPressure = PressureRank(pressureLevel) < PressureRank(currentPressureLevel);
        var betterByOverloadRelief =
            PressureRank(currentPressureLevel) >= PressureRank("high")
            && PressureRank(pressureLevel) < PressureRank("high");
        var isBetter = betterByWorkload || betterByPressure || betterByOverloadRelief;

        var improvementReason =
            betterByOverloadRelief
                ? "Current owner is overloaded while this eligible owner is not."
                : betterByPressure
                    ? "Lower operational pressure among eligible assignees."
                    : "Lower workload among eligible assignees.";

        return new ReassignmentTargetResponse
        {
            UserId = userId,
            OwnerKey = ownerKey,
            DisplayName = ResolveDisplayName(ownerKey, userMatchLookup),
            WorkloadScore = workloadScore,
            PressureLevel = pressureLevel,
            IsBetterThanCurrent = isBetter,
            ImprovementReason = improvementReason,
        };
    }

    private static Dictionary<string, User> BuildUserMatchLookup(IEnumerable<User> users)
    {
        var lookup = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            AddLookupIfMissing(lookup, OwnerFieldResolution.ToCanonicalOwnerKey(user), user);
            AddLookupIfMissing(lookup, user.DisplayName, user);
            AddLookupIfMissing(lookup, user.Email, user);
            AddLookupIfMissing(lookup, user.NickName, user);
        }

        return lookup;
    }

    private static void AddDepartmentDeveloperPool(
        IList<string> ownerKeys,
        IEnumerable<User> users,
        User? requester,
        RoutingDecisionResult routingDecision,
        string assignmentField)
    {
        if (assignmentField != "synitiOwner"
            || routingDecision.OutcomeType != RoutingOutcomeType.RuleMatch
            || !routingDecision.MatchedRuleId.HasValue)
        {
            return;
        }

        var department = NormalizeForLookup(requester?.Department);
        if (department.Length == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            if (!user.IsActive
                || !user.IsSynitiOwnerEligible
                || !string.Equals(user.Role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeForLookup(user.Department), department, StringComparison.Ordinal))
            {
                continue;
            }

            var ownerKey = OwnerFieldResolution.ToCanonicalOwnerKey(user);
            if (!ownerKeys.Contains(ownerKey, StringComparer.OrdinalIgnoreCase))
            {
                ownerKeys.Add(ownerKey);
            }
        }
    }

    private static void AddLookupIfMissing(
        Dictionary<string, User> lookup,
        string? key,
        User user)
    {
        var normalized = NormalizeForLookup(key);
        if (normalized.Length == 0 || lookup.ContainsKey(normalized))
        {
            return;
        }

        lookup[normalized] = user;
    }

    private static string ResolveDisplayName(
        string ownerKey,
        IReadOnlyDictionary<string, User> userMatchLookup)
    {
        var normalized = NormalizeForLookup(ownerKey);
        if (normalized.Length > 0 && userMatchLookup.TryGetValue(normalized, out var user))
        {
            return string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.Email ?? ownerKey
                : user.DisplayName.Trim();
        }

        return ownerKey;
    }

    private static string CanonicalizeOwnerKey(
        string ownerKey,
        IReadOnlyDictionary<string, User> userMatchLookup)
    {
        var normalized = NormalizeForLookup(ownerKey);
        return normalized.Length > 0 && userMatchLookup.TryGetValue(normalized, out var user)
            ? OwnerFieldResolution.ToCanonicalOwnerKey(user)
            : ownerKey;
    }

    private static IEnumerable<string> CollectOwnerKeys(string? synitiOwner, string? businessOwner)
    {
        if (!string.IsNullOrWhiteSpace(synitiOwner))
        {
            yield return synitiOwner.Trim();
        }
        if (!string.IsNullOrWhiteSpace(businessOwner))
        {
            yield return businessOwner.Trim();
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeForLookup(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static int PressureRank(string? pressureLevel)
    {
        return pressureLevel?.ToLowerInvariant() switch
        {
            "critical" => 3,
            "high" => 2,
            "moderate" => 1,
            _ => 0,
        };
    }

    private static string ToPressureLevel(decimal workloadScore) =>
        WorkloadScoringPolicy.ToPressureLevel(workloadScore);

    private static string ResolveAssignmentField(Ticket ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket.SynitiOwner))
        {
            return "synitiOwner";
        }
        if (!string.IsNullOrWhiteSpace(ticket.BusinessOwner))
        {
            return "businessOwner";
        }
        return "unassigned";
    }

    private static ReassignmentRecommendationResponse BuildNoSuggestion(
        string reason,
        string assignmentField)
    {
        return new ReassignmentRecommendationResponse
        {
            ShouldSuggestReassignment = false,
            Reason = reason,
            AssignmentField = assignmentField,
            CurrentOwner = null,
            SuggestedTargets = [],
        };
    }

    private sealed record CandidateAssignment(
        string SynitiOwner,
        string BusinessOwner);
}
