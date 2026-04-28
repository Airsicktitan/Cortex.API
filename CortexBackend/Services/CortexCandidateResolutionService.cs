using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Data;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class CortexCandidateResolutionService(
    Cortex.API.Database.CortexDbContext dbContext,
    IUserRepository userRepository,
    ITicketRoutingRuleService ticketRoutingRuleService,
    IWorkloadSnapshotService workloadSnapshotService) : ICortexCandidateResolutionService
{
    public async Task<IReadOnlyList<CortexDecisionCandidate>> GetEligibleCandidatesAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        var requester = await userRepository.GetByIdAsync(ticket.CreatedBy);
        var factors = new RoutingFactors(
            BoardId: ticket.BoardId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Priority: Normalize(ticket.Priority),
            RequesterDepartment: Normalize(requester?.Department),
            RequesterRole: Normalize(requester?.Role),
            LegacyDepartment: Normalize(requester?.Department),
            LegacyTitle: Normalize(ticket.Title));
        var routing = await ticketRoutingRuleService.EvaluateAsync(factors, ticket.Id, cancellationToken);
        var explanation = ParseExplanation(routing.ExplanationJson);

        var ownerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(routing.RecommendedSynitiOwner))
        {
            ownerKeys.Add(routing.RecommendedSynitiOwner.Trim());
        }

        foreach (var assignment in explanation)
        {
            if (!string.IsNullOrWhiteSpace(assignment.SynitiOwner))
            {
                ownerKeys.Add(assignment.SynitiOwner.Trim());
            }
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .ToListAsync(cancellationToken);
        var userAliases = OwnerFieldResolution.BuildAliasLookup(users);
        AddSynitiEligiblePool(ownerKeys, users);
        if (ownerKeys.Count == 0)
        {
            return [];
        }

        var snapshots = await workloadSnapshotService.GetSnapshotsAsync(cancellationToken);
        var snapshotLookup = BuildSnapshotLookup(snapshots);
        var candidates = new List<CortexDecisionCandidate>();
        foreach (var ownerKey in ownerKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var user = OwnerFieldResolution.ResolveUser(ownerKey, userAliases);
            var canonicalOwnerKey = user is null
                ? ownerKey
                : OwnerFieldResolution.ToCanonicalOwnerKey(user);

            var snapshot = ResolveSnapshot(canonicalOwnerKey, user, snapshotLookup)
                ?? new WorkloadSnapshot
                {
                    UserId = canonicalOwnerKey,
                    DisplayName = user?.DisplayName?.Trim() ?? canonicalOwnerKey,
                    Status = "Available",
                };
            var ruleMatched = explanation.Any(assignment =>
                canonicalOwnerKey.Equals(assignment.SynitiOwner, StringComparison.OrdinalIgnoreCase));

            var candidate = new CortexDecisionCandidate
            {
                UserId = canonicalOwnerKey,
                DisplayName = user?.DisplayName?.Trim() ?? canonicalOwnerKey,
                Eligible = user is not null
                    && OwnerRoleAssignmentRules.IsValidSynitiOwnerAssignment(user),
                ActiveTicketCount = snapshot.ActiveTicketCount,
                HighPriorityCount = snapshot.HighPriorityCount,
                OverdueTicketCount = snapshot.OverdueTicketCount,
                SlaRiskCount = snapshot.SlaRiskCount,
                StaleTicketCount = snapshot.StaleTicketCount,
                WorkloadScore = snapshot.WorkloadScore,
                RuleMatched = ruleMatched,
                PreferredByBoard = ruleMatched && routing.MatchedRuleId.HasValue,
                CurrentlyOverloaded = snapshot.Status == "Overloaded",
            };

            if (candidate.RuleMatched)
            {
                candidate.Notes.Add("Matched routing rule for ticket board.");
            }
            if (candidate.CurrentlyOverloaded)
            {
                candidate.Notes.Add("Currently overloaded.");
            }
            if (candidate.SlaRiskCount == 0)
            {
                candidate.Notes.Add("No SLA-risk tickets assigned.");
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    private static void AddSynitiEligiblePool(
        ISet<string> ownerKeys,
        IEnumerable<User> users)
    {
        foreach (var user in users)
        {
            if (!OwnerRoleAssignmentRules.IsValidSynitiOwnerAssignment(user))
            {
                continue;
            }

            ownerKeys.Add(OwnerFieldResolution.ToCanonicalOwnerKey(user));
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyDictionary<string, WorkloadSnapshot> BuildSnapshotLookup(
        IEnumerable<WorkloadSnapshot> snapshots)
    {
        var lookup = new Dictionary<string, WorkloadSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots)
        {
            AddSnapshotLookup(lookup, snapshot.UserId, snapshot);
            AddSnapshotLookup(lookup, snapshot.DisplayName, snapshot);
        }

        return lookup;
    }

    private static void AddSnapshotLookup(
        IDictionary<string, WorkloadSnapshot> lookup,
        string? key,
        WorkloadSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        lookup.TryAdd(key.Trim(), snapshot);
    }

    private static WorkloadSnapshot? ResolveSnapshot(
        string canonicalOwnerKey,
        User? user,
        IReadOnlyDictionary<string, WorkloadSnapshot> snapshotLookup)
    {
        if (snapshotLookup.TryGetValue(canonicalOwnerKey, out var byOwnerKey))
        {
            return byOwnerKey;
        }

        if (!string.IsNullOrWhiteSpace(user?.DisplayName)
            && snapshotLookup.TryGetValue(user.DisplayName.Trim(), out var byDisplayName))
        {
            return byDisplayName;
        }

        return null;
    }

    private static List<CandidateAssignment> ParseExplanation(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson))
        {
            return [];
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ExplanationPayload>(explanationJson);
            return payload?.CandidateAssignments ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class ExplanationPayload
    {
        [JsonPropertyName("candidateAssignments")]
        public List<CandidateAssignment> CandidateAssignments { get; set; } = [];
    }

    private sealed class CandidateAssignment
    {
        [JsonPropertyName("synitiOwner")]
        public string? SynitiOwner { get; set; }
    }
}
