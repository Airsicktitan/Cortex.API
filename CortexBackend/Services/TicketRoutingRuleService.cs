using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cortex.API.Services;

public class TicketRoutingRuleService(
    ITicketRoutingRuleRepository repository,
    CortexDbContext dbContext,
    IOwnerWorkloadScoringService ownerWorkloadScoringService) : ITicketRoutingRuleService
{
    private readonly ITicketRoutingRuleRepository _repository = repository;
    private readonly CortexDbContext _dbContext = dbContext;
    private readonly IOwnerWorkloadScoringService _ownerWorkloadScoringService = ownerWorkloadScoringService;
    private const string EngineVersion = "decision-engine-v1";

    public async Task<IReadOnlyList<TicketRoutingRule>> GetAllAsync()
    {
        var rules = await _repository.GetAllAsync();
        return rules.Select(Clone).ToList();
    }

    public async Task<TicketRoutingRule> CreateAsync(TicketRoutingRule rule)
    {
        var normalizedRule = Normalize(rule);
        await ValidateAsync(normalizedRule, null);

        await _repository.AddAsync(normalizedRule);
        await _repository.SaveChangesAsync();

        return Clone(normalizedRule);
    }

    public async Task<TicketRoutingRule> UpdateAsync(int id, TicketRoutingRule rule)
    {
        var existingRule = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket routing rule was not found.");

        var normalizedRule = Normalize(rule);
        await ValidateAsync(normalizedRule, id);

        existingRule.Department = normalizedRule.Department;
        existingRule.TitleContains = normalizedRule.TitleContains;
        existingRule.BoardId = normalizedRule.BoardId;
        existingRule.Priority = normalizedRule.Priority;
        existingRule.RequesterDepartment = normalizedRule.RequesterDepartment;
        existingRule.RequesterRole = normalizedRule.RequesterRole;
        existingRule.RulePriority = normalizedRule.RulePriority;
        existingRule.Weight = normalizedRule.Weight;
        existingRule.SynitiOwner = normalizedRule.SynitiOwner;
        existingRule.BusinessOwner = normalizedRule.BusinessOwner;
        existingRule.IsEnabled = normalizedRule.IsEnabled;
        existingRule.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();
        return Clone(existingRule);
    }

    public async Task DeleteAsync(int id)
    {
        var existingRule = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket routing rule was not found.");

        _repository.Delete(existingRule);
        await _repository.SaveChangesAsync();
    }

    public async Task<TicketRoutingResolution> ResolveOwnersAsync(string? department, string? title)
    {
        var result = await EvaluateAsync(new RoutingFactors(
            BoardId: null,
            Priority: null,
            RequesterDepartment: null,
            RequesterRole: null,
            LegacyDepartment: department,
            LegacyTitle: title));

        return new TicketRoutingResolution(result.RecommendedSynitiOwner, result.RecommendedBusinessOwner);
    }

    public Task<RoutingDecisionResult> EvaluateAsync(
        RoutingFactors factors,
        CancellationToken cancellationToken = default)
    {
        return EvaluateAsync(factors, excludeTicketId: null, cancellationToken);
    }

    public async Task<RoutingDecisionResult> EvaluateAsync(
        RoutingFactors factors,
        string? excludeTicketId,
        CancellationToken cancellationToken = default)
    {
        var allRules = await _repository.GetAllAsync();
        if (allRules.Count == 0)
        {
            return BuildFallback(factors, RoutingNoMatchReason.NoRulesDefined, "No routing rules are defined.");
        }

        var enabledRules = allRules.Where(rule => rule.IsEnabled).ToList();
        if (enabledRules.Count == 0)
        {
            return BuildFallback(factors, RoutingNoMatchReason.NoEnabledRules, "Routing rules exist but all are disabled.");
        }

        var normalizedFactors = NormalizeFactors(factors);
        var candidateMatches = enabledRules
            .Select(rule => EvaluateRuleMatch(rule, normalizedFactors))
            .Where(match => match.IsMatch)
            .ToList();

        if (candidateMatches.Count == 0)
        {
            var noMatchReason = HasMeaningfulFactors(normalizedFactors)
                ? RoutingNoMatchReason.NoCriteriaMatched
                : RoutingNoMatchReason.MissingRequiredFactors;
            var text = noMatchReason == RoutingNoMatchReason.MissingRequiredFactors
                ? "Routing factors required by rules were missing."
                : "No enabled rule matched the routing factors.";
            return BuildFallback(normalizedFactors, noMatchReason, text);
        }

        var userDirectory = await _dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var userAliases = OwnerFieldResolution.BuildAliasLookup(userDirectory);
        var candidateOwnerKeys = candidateMatches
            .SelectMany(match => GetRuleOwnerKeys(match.Rule))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var ownerScores = await _ownerWorkloadScoringService.GetScoresAsync(
            candidateOwnerKeys,
            excludeTicketId,
            respectCurrentVisibility: false,
            cancellationToken);
        var ownerScoreMap = ownerScores.ToDictionary(score => score.OwnerKey, StringComparer.OrdinalIgnoreCase);

        var synitiSlot = EvaluateSlotCandidates(
            slotName: "synitiOwner",
            candidateMatches,
            userAliases,
            ownerScoreMap,
            isSynitiSlot: true);
        var businessSlot = EvaluateSlotCandidates(
            slotName: "businessOwner",
            candidateMatches,
            userAliases,
            ownerScoreMap,
            isSynitiSlot: false);

        var matchedRuleId = ResolveMatchedRuleId(synitiSlot, businessSlot);
        var highestMatchedCriteriaCount = Math.Max(
            synitiSlot.Selected?.MatchedCriteriaCount ?? 0,
            businessSlot.Selected?.MatchedCriteriaCount ?? 0);
        var highestMatchScore = Math.Max(
            synitiSlot.Selected?.MatchScore ?? 0,
            businessSlot.Selected?.MatchScore ?? 0);
        var confidence = highestMatchScore >= 70
            ? RoutingConfidenceLevel.High
            : highestMatchScore >= 40
                ? RoutingConfidenceLevel.Medium
                : RoutingConfidenceLevel.Low;
        var precedenceScore = Math.Max(
            synitiSlot.Selected?.FinalScore ?? 0,
            businessSlot.Selected?.FinalScore ?? 0);
        var tieBreakKey =
            $"{synitiSlot.Selected?.FinalScore ?? int.MinValue:D6}|{businessSlot.Selected?.FinalScore ?? int.MinValue:D6}|{matchedRuleId?.ToString() ?? "none"}";
        var matchedCriteria = new[] { synitiSlot.Selected, businessSlot.Selected }
            .Where(candidate => candidate is not null)
            .Cast<SlotCandidateEvaluation>()
            .SelectMany(candidate => candidate.MatchedCriteria)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var allCandidateAssignments = candidateMatches.Select(match =>
        {
            var synitiOwner = NormalizeOptionalValue(match.Rule.SynitiOwner);
            var businessOwner = NormalizeOptionalValue(match.Rule.BusinessOwner);
            var canonicalSynitiOwner = OwnerFieldResolution.CanonicalizeOwnerField(synitiOwner, userAliases);
            var canonicalBusinessOwner = OwnerFieldResolution.CanonicalizeOwnerField(businessOwner, userAliases);
            var ruleOwnerScores = new List<OwnerWorkloadScoreSnapshot>();
            if (!string.IsNullOrWhiteSpace(synitiOwner)
                && ownerScoreMap.TryGetValue(synitiOwner, out var synitiScore))
            {
                ruleOwnerScores.Add(synitiScore);
            }
            if (!string.IsNullOrWhiteSpace(businessOwner)
                && ownerScoreMap.TryGetValue(businessOwner, out var businessScore)
                && !ruleOwnerScores.Any(existing =>
                    string.Equals(existing.OwnerKey, businessScore.OwnerKey, StringComparison.OrdinalIgnoreCase)))
            {
                ruleOwnerScores.Add(businessScore);
            }

            return new
            {
                matchedRuleId = match.Rule.Id,
                synitiOwner = canonicalSynitiOwner,
                businessOwner = canonicalBusinessOwner,
                combinedAssignmentWorkloadScore = ruleOwnerScores.Sum(score => score.WorkloadScore),
                ownerScores = ruleOwnerScores.Select(ToWorkloadExplanation)
            };
        });

        var explanationObject = new
        {
            engine = EngineVersion,
            decisionType = "workload_aware_routing_v1",
            formula = "finalScore = matchScore - workloadPenalty",
            autoAssignmentThreshold = 40,
            weakSignalThreshold = 35,
            workloadPenaltyCap = 30,
            confidenceClassification = ResolveOverallClassification(synitiSlot, businessSlot),
            matchedRuleId,
            factors = normalizedFactors,
            matchedCriteria,
            slots = new
            {
                synitiOwner = ToSlotExplanation(synitiSlot),
                businessOwner = ToSlotExplanation(businessSlot)
            },
            candidateAssignments = allCandidateAssignments
        };

        var explanationText = BuildExplanationText(synitiSlot, businessSlot);

        return new RoutingDecisionResult(
            MatchedRuleId: matchedRuleId,
            OutcomeType: RoutingOutcomeType.RuleMatch,
            ConfidenceLevel: confidence,
            NoMatchReason: null,
            RecommendedSynitiOwner: synitiSlot.Applied ? synitiSlot.Selected?.OwnerKey : null,
            RecommendedBusinessOwner: businessSlot.Applied ? businessSlot.Selected?.OwnerKey : null,
            PrecedenceScore: precedenceScore,
            TieBreakKey: tieBreakKey,
            ExplanationJson: JsonSerializer.Serialize(explanationObject),
            ExplanationText: explanationText,
            EngineVersion: EngineVersion,
            MatchedCriteriaCount: highestMatchedCriteriaCount);
    }

    public async Task<TicketRoutingDecision> RecordDecisionAsync(
        string ticketId,
        RoutingDecisionResult decision,
        CancellationToken cancellationToken = default)
    {
        var ownerAliases = await BuildOwnerAliasLookupAsync(cancellationToken);
        var entity = new TicketRoutingDecision
        {
            TicketId = ticketId,
            MatchedRuleId = decision.MatchedRuleId,
            OutcomeType = decision.OutcomeType,
            ConfidenceLevel = decision.ConfidenceLevel,
            NoMatchReason = decision.NoMatchReason,
            ChosenSynitiOwner = CanonicalizeOwnerForPersistence(decision.RecommendedSynitiOwner, ownerAliases),
            ChosenBusinessOwner = CanonicalizeOwnerForPersistence(decision.RecommendedBusinessOwner, ownerAliases),
            PrecedenceScore = decision.PrecedenceScore,
            TieBreakKey = decision.TieBreakKey,
            ExplanationJson = decision.ExplanationJson,
            ExplanationText = decision.ExplanationText,
            EngineVersion = decision.EngineVersion
        };

        await _dbContext.TicketRoutingDecisions.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<TicketRoutingOverride> RecordOverrideAsync(
        string ticketId,
        int overriddenByUserId,
        string? previousSynitiOwner,
        string? previousBusinessOwner,
        string? newSynitiOwner,
        string? newBusinessOwner,
        RoutingOverrideReasonType reasonType,
        string? reasonText,
        DecisionImpactSnapshot? decisionImpactSnapshot = null,
        CancellationToken cancellationToken = default)
    {
        var ownerAliases = await BuildOwnerAliasLookupAsync(cancellationToken);
        var entity = new TicketRoutingOverride
        {
            TicketId = ticketId,
            OverriddenByUserId = overriddenByUserId,
            PreviousSynitiOwner = CanonicalizeOwnerForPersistence(previousSynitiOwner, ownerAliases),
            PreviousBusinessOwner = CanonicalizeOwnerForPersistence(previousBusinessOwner, ownerAliases),
            NewSynitiOwner = CanonicalizeOwnerForPersistence(newSynitiOwner, ownerAliases),
            NewBusinessOwner = CanonicalizeOwnerForPersistence(newBusinessOwner, ownerAliases),
            OverrideReasonType = reasonType,
            OverrideReasonText = NormalizeOptionalValue(reasonText)
        };

        if (decisionImpactSnapshot is not null)
        {
            entity.DecisionImpactPreviousOwnerId = decisionImpactSnapshot.PreviousOwnerId;
            entity.DecisionImpactAssignmentField = NormalizeOptionalValue(decisionImpactSnapshot.AssignmentField);
            entity.DecisionImpactPreviousOwnerWorkload = decisionImpactSnapshot.PreviousOwnerWorkload;
            entity.DecisionImpactPreviousPressureLevel = NormalizeOptionalValue(decisionImpactSnapshot.PreviousPressureLevel);
            entity.DecisionImpactPreviousRiskLevel = NormalizeOptionalValue(decisionImpactSnapshot.PreviousRiskLevel);
            entity.DecisionImpactPreviousSlaStatus = NormalizeOptionalValue(decisionImpactSnapshot.PreviousSlaStatus);
            entity.DecisionImpactAppliedAtUtc = decisionImpactSnapshot.AppliedAtUtc;
            entity.DecisionImpactSource = NormalizeOptionalValue(decisionImpactSnapshot.Source);
        }

        await _dbContext.TicketRoutingOverrides.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<TicketRoutingDecision?> GetLatestDecisionAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TicketRoutingDecisions
            .AsNoTracking()
            .OrderByDescending(decision => decision.CreatedDateUtc)
            .ThenByDescending(decision => decision.Id)
            .FirstOrDefaultAsync(decision => decision.TicketId == ticketId, cancellationToken);
    }

    public Task<TicketRoutingOverride?> GetLatestOverrideAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.TicketRoutingOverrides
            .AsNoTracking()
            .OrderByDescending(@override => @override.CreatedDateUtc)
            .ThenByDescending(@override => @override.Id)
            .Select(@override => new TicketRoutingOverride
            {
                Id = @override.Id,
                TicketId = @override.TicketId,
                OverriddenByUserId = @override.OverriddenByUserId,
                PreviousSynitiOwner = @override.PreviousSynitiOwner,
                PreviousBusinessOwner = @override.PreviousBusinessOwner,
                NewSynitiOwner = @override.NewSynitiOwner,
                NewBusinessOwner = @override.NewBusinessOwner,
                OverrideReasonType = @override.OverrideReasonType,
                OverrideReasonText = @override.OverrideReasonText,
                CreatedDateUtc = @override.CreatedDateUtc,
            })
            .FirstOrDefaultAsync(@override => @override.TicketId == ticketId, cancellationToken);
    }

    private async Task ValidateAsync(TicketRoutingRule rule, int? existingId)
    {
        if (rule.RulePriority < 0)
        {
            throw new ArgumentException("RulePriority must be zero or greater.", nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(rule.Department)
            && string.IsNullOrWhiteSpace(rule.TitleContains)
            && string.IsNullOrWhiteSpace(rule.BoardId)
            && string.IsNullOrWhiteSpace(rule.Priority)
            && string.IsNullOrWhiteSpace(rule.RequesterDepartment)
            && string.IsNullOrWhiteSpace(rule.RequesterRole))
        {
            throw new ArgumentException(
                "Add at least one routing criterion.",
                nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(rule.SynitiOwner)
            && string.IsNullOrWhiteSpace(rule.BusinessOwner))
        {
            throw new ArgumentException(
                "Add a Syniti owner, a business owner, or both.",
                nameof(rule));
        }

        var existingRules = await _repository.GetAllAsync();
        var duplicateRule = existingRules.FirstOrDefault(existingRule =>
            existingRule.Id != existingId
            && string.Equals(
                NormalizeLookupValue(existingRule.BoardId),
                NormalizeLookupValue(rule.BoardId),
                StringComparison.Ordinal)
            && string.Equals(
                NormalizeLookupValue(existingRule.Priority),
                NormalizeLookupValue(rule.Priority),
                StringComparison.Ordinal)
            && string.Equals(
                NormalizeLookupValue(existingRule.RequesterDepartment),
                NormalizeLookupValue(rule.RequesterDepartment),
                StringComparison.Ordinal)
            && string.Equals(
                NormalizeLookupValue(existingRule.RequesterRole),
                NormalizeLookupValue(rule.RequesterRole),
                StringComparison.Ordinal)
            && string.Equals(
                NormalizeLookupValue(existingRule.Department),
                NormalizeLookupValue(rule.Department),
                StringComparison.Ordinal)
            && string.Equals(
                NormalizeLookupValue(existingRule.TitleContains),
                NormalizeLookupValue(rule.TitleContains),
                StringComparison.Ordinal));

        if (duplicateRule is not null)
        {
            throw new ArgumentException(
                "A routing rule with the same criteria already exists.",
                nameof(rule));
        }
    }

    private static TicketRoutingRule Normalize(TicketRoutingRule rule)
    {
        return new TicketRoutingRule
        {
            BoardId = NormalizeOptionalValue(rule.BoardId),
            Priority = NormalizeOptionalValue(rule.Priority),
            RequesterDepartment = NormalizeOptionalValue(rule.RequesterDepartment),
            RequesterRole = NormalizeOptionalValue(rule.RequesterRole),
            RulePriority = rule.RulePriority,
            Weight = rule.Weight,
            Department = NormalizeOptionalValue(rule.Department),
            TitleContains = NormalizeOptionalValue(rule.TitleContains),
            SynitiOwner = NormalizeOptionalValue(rule.SynitiOwner),
            BusinessOwner = NormalizeOptionalValue(rule.BusinessOwner),
            IsEnabled = rule.IsEnabled,
            CreatedDateUtc = rule.CreatedDateUtc == default
                ? DateTime.UtcNow
                : rule.CreatedDateUtc,
            LastModifiedDateUtc = rule.LastModifiedDateUtc
        };
    }

    private static TicketRoutingRule Clone(TicketRoutingRule rule)
    {
        return new TicketRoutingRule
        {
            Id = rule.Id,
            BoardId = rule.BoardId,
            Priority = rule.Priority,
            RequesterDepartment = rule.RequesterDepartment,
            RequesterRole = rule.RequesterRole,
            RulePriority = rule.RulePriority,
            Weight = rule.Weight,
            Department = rule.Department,
            TitleContains = rule.TitleContains,
            SynitiOwner = rule.SynitiOwner,
            BusinessOwner = rule.BusinessOwner,
            IsEnabled = rule.IsEnabled,
            CreatedDateUtc = rule.CreatedDateUtc,
            LastModifiedDateUtc = rule.LastModifiedDateUtc
        };
    }

    private static RuleMatchResult EvaluateRuleMatch(TicketRoutingRule rule, RoutingFactors factors)
    {
        var matchedCriteria = new List<string>();
        if (!MatchesRuleCriterion(rule.BoardId, factors.BoardId, "BoardId", matchedCriteria)) return RuleMatchResult.NoMatch(rule);
        if (!MatchesRuleCriterion(rule.Priority, factors.Priority, "Priority", matchedCriteria)) return RuleMatchResult.NoMatch(rule);
        if (!MatchesRuleCriterion(rule.RequesterDepartment, factors.RequesterDepartment, "RequesterDepartment", matchedCriteria)) return RuleMatchResult.NoMatch(rule);
        if (!MatchesRuleCriterion(rule.RequesterRole, factors.RequesterRole, "RequesterRole", matchedCriteria)) return RuleMatchResult.NoMatch(rule);
        if (!MatchesRuleCriterion(rule.Department, factors.LegacyDepartment, "Department", matchedCriteria)) return RuleMatchResult.NoMatch(rule);

        if (!string.IsNullOrWhiteSpace(rule.TitleContains))
        {
            if (string.IsNullOrWhiteSpace(factors.LegacyTitle))
            {
                return RuleMatchResult.NoMatch(rule);
            }

            var titleCriterion = NormalizeLookupValue(rule.TitleContains);
            if (titleCriterion is null || !factors.LegacyTitle!.Contains(titleCriterion, StringComparison.Ordinal))
            {
                return RuleMatchResult.NoMatch(rule);
            }

            matchedCriteria.Add("TitleContains");
        }

        return new RuleMatchResult(rule, true, matchedCriteria);
    }

    private static bool MatchesRuleCriterion(
        string? criterion,
        string? factor,
        string label,
        List<string> matchedCriteria)
    {
        if (string.IsNullOrWhiteSpace(criterion))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(factor))
        {
            return false;
        }

        if (!string.Equals(
                NormalizeLookupValue(criterion),
                NormalizeLookupValue(factor),
                StringComparison.Ordinal))
        {
            return false;
        }

        matchedCriteria.Add(label);
        return true;
    }

    private static IEnumerable<string> GetRuleOwnerKeys(TicketRoutingRule rule)
    {
        return new string?[]
        {
            NormalizeOptionalValue(rule.SynitiOwner),
            NormalizeOptionalValue(rule.BusinessOwner)
        }
        .Where(ownerKey => !string.IsNullOrWhiteSpace(ownerKey))
        .Select(ownerKey => ownerKey!)
        .Distinct(StringComparer.Ordinal);
    }

    private static object ToWorkloadExplanation(OwnerWorkloadScoreSnapshot score)
    {
        return new
        {
            ownerKey = score.OwnerKey,
            score = score.WorkloadScore,
            activeTicketCount = score.ActiveTicketCount,
            highPriorityTicketCount = score.HighPriorityTicketCount,
            atRiskTicketCount = score.AtRiskTicketCount,
            outsideSlaOpenCount = score.OutsideSlaOpenCount,
            slaRiskTicketCount = score.SlaRiskTicketCount,
            staleTicketCount = score.StaleTicketCount
        };
    }

    private static SlotDecisionResult EvaluateSlotCandidates(
        string slotName,
        IReadOnlyList<RuleMatchResult> candidateMatches,
        IReadOnlyDictionary<string, User> userAliases,
        IReadOnlyDictionary<string, OwnerWorkloadScoreSnapshot> ownerScoreMap,
        bool isSynitiSlot)
    {
        var candidatesByUserId = new Dictionary<int, SlotCandidateDraft>();
        var skippedReasons = new List<SlotSkippedOwner>();

        foreach (var match in candidateMatches)
        {
            var ownerKey = NormalizeOptionalValue(
                isSynitiSlot ? match.Rule.SynitiOwner : match.Rule.BusinessOwner);
            if (ownerKey is null)
            {
                skippedReasons.Add(new SlotSkippedOwner(
                    RuleId: match.Rule.Id,
                    OwnerKey: null,
                    UserId: null,
                    Reason: "RuleMissingOwner",
                    Message: "Rule does not define an owner for this assignment slot."));
                continue;
            }

            var user = OwnerFieldResolution.ResolveUser(ownerKey, userAliases);
            if (user is null)
            {
                skippedReasons.Add(new SlotSkippedOwner(
                    RuleId: match.Rule.Id,
                    OwnerKey: ownerKey,
                    UserId: null,
                    Reason: "UnresolvedRuleOwner",
                    Message: "Rule target could not be resolved to an active user."));
                continue;
            }

            if (!user.IsActive)
            {
                skippedReasons.Add(new SlotSkippedOwner(
                    RuleId: match.Rule.Id,
                    OwnerKey: ownerKey,
                    UserId: user.Id,
                    Reason: "InactiveUser",
                    Message: "Rule target could not be resolved to an active user."));
                continue;
            }

            if (isSynitiSlot && !OwnerRoleAssignmentRules.IsValidSynitiOwnerAssignment(user))
            {
                skippedReasons.Add(new SlotSkippedOwner(
                    RuleId: match.Rule.Id,
                    OwnerKey: ownerKey,
                    UserId: user.Id,
                    Reason: "InvalidSynitiOwnerRole",
                    Message: $"Rule target must be an active user in department '{UserDepartmentPolicy.DefaultDeveloperDepartment}' and eligible as Syniti owner."));
                continue;
            }

            if (!isSynitiSlot && !OwnerRoleAssignmentRules.IsValidBusinessOwnerAssignment(user))
            {
                skippedReasons.Add(new SlotSkippedOwner(
                    RuleId: match.Rule.Id,
                    OwnerKey: ownerKey,
                    UserId: user.Id,
                    Reason: "InvalidBusinessOwnerRole",
                    Message: "Rule target must be an active non-developer, non-guest user eligible as business owner."));
                continue;
            }

            var canonicalOwnerKey = OwnerFieldResolution.ToCanonicalOwnerKey(user);
            var snapshot = ownerScoreMap.TryGetValue(ownerKey, out var scored)
                ? scored
                : new OwnerWorkloadScoreSnapshot(ownerKey, 0, 0, 0, 0, 0, 0);
            var matchScore = ComputeMatchScore(match.Rule, match.MatchedCriteria);
            var workloadPenalty = ComputeWorkloadPenalty(snapshot);
            var candidate = new SlotCandidateDraft(
                UserId: user.Id,
                OwnerKey: canonicalOwnerKey,
                DisplayName: ResolveUserDisplayName(user),
                RuleId: match.Rule.Id,
                MatchScore: matchScore,
                WorkloadPenalty: workloadPenalty,
                ActiveTicketCount: snapshot.ActiveTicketCount,
                HighPriorityTicketCount: snapshot.HighPriorityTicketCount,
                AtRiskTicketCount: snapshot.AtRiskTicketCount,
                OutsideSlaOpenCount: snapshot.OutsideSlaOpenCount,
                SlaRiskTicketCount: snapshot.SlaRiskTicketCount,
                MatchedCriteriaCount: match.MatchedCriteriaCount,
                MatchedCriteria: match.MatchedCriteria,
                RulePriority: match.Rule.RulePriority,
                RuleWeight: match.Rule.Weight);

            if (!candidatesByUserId.TryGetValue(user.Id, out var existing)
                || IsBetterCandidate(candidate, existing))
            {
                candidatesByUserId[user.Id] = candidate;
            }
        }

        var ranked = candidatesByUserId.Values
            .Select(candidate => new SlotCandidateEvaluation(
                UserId: candidate.UserId,
                OwnerKey: candidate.OwnerKey,
                DisplayName: candidate.DisplayName,
                RuleId: candidate.RuleId,
                MatchScore: candidate.MatchScore,
                WorkloadPenalty: candidate.WorkloadPenalty,
                FinalScore: candidate.MatchScore - candidate.WorkloadPenalty,
                ActiveTicketCount: candidate.ActiveTicketCount,
                HighPriorityTicketCount: candidate.HighPriorityTicketCount,
                AtRiskTicketCount: candidate.AtRiskTicketCount,
                OutsideSlaOpenCount: candidate.OutsideSlaOpenCount,
                SlaRiskTicketCount: candidate.SlaRiskTicketCount,
                MatchedCriteriaCount: candidate.MatchedCriteriaCount,
                MatchedCriteria: candidate.MatchedCriteria,
                RulePriority: candidate.RulePriority,
                RuleWeight: candidate.RuleWeight))
            .OrderByDescending(candidate => candidate.FinalScore)
            .ThenByDescending(candidate => candidate.MatchScore)
            .ThenBy(candidate => candidate.WorkloadPenalty)
            .ThenBy(candidate => candidate.ActiveTicketCount)
            .ThenBy(candidate => candidate.UserId)
            .ToList();
        var selected = ranked.FirstOrDefault();
        if (selected is null)
        {
            return new SlotDecisionResult(
                Slot: slotName,
                Applied: false,
                Classification: "Limited routing signals",
                AppliedReason: "No eligible owner candidates were available.",
                Selected: null,
                Candidates: ranked,
                SkippedReasons: skippedReasons);
        }

        var weakSignal = selected.MatchScore < 35;
        var tie = ranked.Count > 1 && Math.Abs(selected.FinalScore - ranked[1].FinalScore) <= 5;
        var applied = selected.MatchScore >= 40 && !weakSignal && !tie;
        var classification = tie
            ? "Multiple viable candidates"
            : weakSignal
                ? "Limited routing signals"
                : ClassifySignal(selected.MatchScore, selected.WorkloadPenalty);
        var appliedReason = ResolveAppliedReason(selected, weakSignal, tie, applied);

        return new SlotDecisionResult(
            Slot: slotName,
            Applied: applied,
            Classification: classification,
            AppliedReason: appliedReason,
            Selected: selected,
            Candidates: ranked,
            SkippedReasons: skippedReasons);
    }

    private static int? ResolveMatchedRuleId(
        SlotDecisionResult synitiSlot,
        SlotDecisionResult businessSlot)
    {
        var selectedCandidates = new[] { synitiSlot.Selected, businessSlot.Selected }
            .Where(candidate => candidate is not null)
            .Cast<SlotCandidateEvaluation>()
            .OrderByDescending(candidate => candidate.FinalScore)
            .ThenByDescending(candidate => candidate.MatchScore)
            .ThenBy(candidate => candidate.WorkloadPenalty)
            .ToList();
        return selectedCandidates.Count == 0 ? null : selectedCandidates[0].RuleId;
    }

    private static object ToSlotExplanation(SlotDecisionResult slot)
    {
        return new
        {
            selectedOwnerId = slot.Selected?.UserId,
            selectedOwnerKey = slot.Selected?.OwnerKey,
            selectedOwnerDisplayName = slot.Selected?.DisplayName,
            applied = slot.Applied,
            appliedReason = slot.AppliedReason,
            classification = slot.Classification,
            candidates = slot.Candidates.Select(candidate => new
            {
                userId = candidate.UserId,
                ownerKey = candidate.OwnerKey,
                displayName = candidate.DisplayName,
                ruleId = candidate.RuleId,
                matchScore = candidate.MatchScore,
                workloadPenalty = candidate.WorkloadPenalty,
                finalScore = candidate.FinalScore,
                activeTicketCount = candidate.ActiveTicketCount,
                highPriorityTicketCount = candidate.HighPriorityTicketCount,
                atRiskTicketCount = candidate.AtRiskTicketCount,
                outsideSlaOpenCount = candidate.OutsideSlaOpenCount,
                slaRiskTicketCount = candidate.SlaRiskTicketCount,
                matchedCriteria = candidate.MatchedCriteria,
                reason = BuildCandidateExplanationReason(candidate, slot)
            }),
            skippedReasons = slot.SkippedReasons.Select(skipped => new
            {
                ruleId = skipped.RuleId,
                ownerKey = skipped.OwnerKey,
                userId = skipped.UserId,
                reason = skipped.Reason,
                message = skipped.Message
            })
        };
    }

    private static string BuildExplanationText(
        SlotDecisionResult synitiSlot,
        SlotDecisionResult businessSlot)
    {
        return
            $"Decision engine evaluated slots independently: Syniti={synitiSlot.Classification}, Business={businessSlot.Classification}.";
    }

    private static string ResolveOverallClassification(
        SlotDecisionResult synitiSlot,
        SlotDecisionResult businessSlot)
    {
        var classifications = new[] { synitiSlot.Classification, businessSlot.Classification };
        if (classifications.Contains("Multiple viable candidates", StringComparer.Ordinal))
        {
            return "Multiple viable candidates";
        }

        if (classifications.Contains("Strong match, low pressure", StringComparer.Ordinal))
        {
            return "Strong match, low pressure";
        }

        if (classifications.Contains("Strong match, moderate pressure", StringComparer.Ordinal))
        {
            return "Strong match, moderate pressure";
        }

        if (classifications.Contains("Moderate match", StringComparer.Ordinal))
        {
            return "Moderate match";
        }

        return "Limited routing signals";
    }

    private static string ClassifySignal(int matchScore, int workloadPenalty)
    {
        if (matchScore >= 70 && workloadPenalty <= 10)
        {
            return "Strong match, low pressure";
        }
        if (matchScore >= 70)
        {
            return "Strong match, moderate pressure";
        }
        return "Moderate match";
    }

    private static string ResolveUserDisplayName(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(user.Email)
            ? $"User #{user.Id}"
            : user.Email.Trim();
    }

    private static string ResolveAppliedReason(
        SlotCandidateEvaluation selected,
        bool weakSignal,
        bool tie,
        bool applied)
    {
        if (applied)
        {
            return "Selected as the best eligible owner for this slot.";
        }

        if (weakSignal)
        {
            return "Limited routing signals available for this ticket.";
        }

        if (tie)
        {
            return "Multiple viable candidates are within the narrow-margin threshold.";
        }

        return selected.MatchScore < 40
            ? "Match score is below the auto-assignment threshold."
            : "Recommendation is advisory until the assignment slot can be safely applied.";
    }

    private static string BuildCandidateExplanationReason(
        SlotCandidateEvaluation candidate,
        SlotDecisionResult slot)
    {
        if (slot.Selected is null)
        {
            return "Not selected because no eligible owner could be ranked.";
        }

        if (candidate.UserId == slot.Selected.UserId)
        {
            return slot.Applied
                ? "Selected: highest final score among eligible candidates."
                : $"Advisory: {slot.AppliedReason}";
        }

        if (candidate.MatchScore < slot.Selected.MatchScore)
        {
            return "Not selected: weaker routing match than selected candidate.";
        }

        if (candidate.WorkloadPenalty > slot.Selected.WorkloadPenalty)
        {
            return "Not selected: higher workload pressure than selected candidate.";
        }

        return "Not selected: lower final score after workload pressure was applied.";
    }

    private static int ComputeMatchScore(TicketRoutingRule rule, IReadOnlyList<string> matchedCriteria)
    {
        var score = 0;
        foreach (var criterion in matchedCriteria)
        {
            score += criterion switch
            {
                "BoardId" => 18,
                "Priority" => 16,
                "RequesterDepartment" => 14,
                "RequesterRole" => 12,
                "Department" => 5,
                "TitleContains" => 5,
                _ => 0
            };
        }

        score += Math.Min(20, (int)Math.Round(Math.Max(0, rule.RulePriority) * 0.4, MidpointRounding.AwayFromZero));
        score += Math.Min(10, (int)Math.Round(Math.Max(0, rule.Weight) * 0.5, MidpointRounding.AwayFromZero));
        return Math.Min(100, score);
    }

    private static int ComputeWorkloadPenalty(OwnerWorkloadScoreSnapshot snapshot)
    {
        var penalty = Math.Min(30m, snapshot.WorkloadScore);
        return (int)Math.Round(penalty, MidpointRounding.AwayFromZero);
    }

    private static bool IsBetterCandidate(
        SlotCandidateDraft candidate,
        SlotCandidateDraft existing)
    {
        return candidate.MatchScore > existing.MatchScore
            || (candidate.MatchScore == existing.MatchScore && candidate.WorkloadPenalty < existing.WorkloadPenalty)
            || (candidate.MatchScore == existing.MatchScore
                && candidate.WorkloadPenalty == existing.WorkloadPenalty
                && candidate.RuleId < existing.RuleId);
    }

    private static RoutingDecisionResult BuildFallback(
        RoutingFactors factors,
        RoutingNoMatchReason noMatchReason,
        string text)
    {
        var explanationObject = new
        {
            engine = EngineVersion,
            formula = "finalScore = matchScore - workloadPenalty",
            matchedRuleId = (int?)null,
            factors,
            noMatchReason = noMatchReason.ToString()
        };

        return new RoutingDecisionResult(
            MatchedRuleId: null,
            OutcomeType: RoutingOutcomeType.Fallback,
            ConfidenceLevel: RoutingConfidenceLevel.Low,
            NoMatchReason: noMatchReason,
            RecommendedSynitiOwner: null,
            RecommendedBusinessOwner: null,
            PrecedenceScore: 0,
            TieBreakKey: "fallback",
            ExplanationJson: JsonSerializer.Serialize(explanationObject),
            ExplanationText: text,
            EngineVersion: EngineVersion,
            MatchedCriteriaCount: 0);
    }

    private static RoutingFactors NormalizeFactors(RoutingFactors factors)
    {
        return factors with
        {
            BoardId = NormalizeLookupValue(factors.BoardId),
            Priority = NormalizeLookupValue(factors.Priority),
            RequesterDepartment = NormalizeLookupValue(factors.RequesterDepartment),
            RequesterRole = NormalizeLookupValue(factors.RequesterRole),
            LegacyDepartment = NormalizeLookupValue(factors.LegacyDepartment),
            LegacyTitle = NormalizeLookupValue(factors.LegacyTitle)
        };
    }

    private static bool HasMeaningfulFactors(RoutingFactors factors)
    {
        return !string.IsNullOrWhiteSpace(factors.BoardId)
            || !string.IsNullOrWhiteSpace(factors.Priority)
            || !string.IsNullOrWhiteSpace(factors.RequesterDepartment)
            || !string.IsNullOrWhiteSpace(factors.RequesterRole)
            || !string.IsNullOrWhiteSpace(factors.LegacyDepartment)
            || !string.IsNullOrWhiteSpace(factors.LegacyTitle);
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeLookupValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private async Task<IReadOnlyDictionary<string, User>> BuildOwnerAliasLookupAsync(
        CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return OwnerFieldResolution.BuildAliasLookup(users);
    }

    private static string? CanonicalizeOwnerForPersistence(
        string? ownerKey,
        IReadOnlyDictionary<string, User> ownerAliases)
    {
        return OwnerFieldResolution.CanonicalizeOwnerField(ownerKey, ownerAliases);
    }

    private sealed record RuleMatchResult(
        TicketRoutingRule Rule,
        bool IsMatch,
        IReadOnlyList<string> MatchedCriteria)
    {
        public int MatchedCriteriaCount => MatchedCriteria.Count;
        public static RuleMatchResult NoMatch(TicketRoutingRule rule) => new(rule, false, []);
    }

    private sealed record SlotCandidateEvaluation(
        int UserId,
        string OwnerKey,
        string DisplayName,
        int RuleId,
        int MatchScore,
        int WorkloadPenalty,
        int FinalScore,
        int ActiveTicketCount,
        int HighPriorityTicketCount,
        int AtRiskTicketCount,
        int OutsideSlaOpenCount,
        int SlaRiskTicketCount,
        int MatchedCriteriaCount,
        IReadOnlyList<string> MatchedCriteria,
        int RulePriority,
        int RuleWeight);

    private sealed record SlotCandidateDraft(
        int UserId,
        string OwnerKey,
        string DisplayName,
        int RuleId,
        int MatchScore,
        int WorkloadPenalty,
        int ActiveTicketCount,
        int HighPriorityTicketCount,
        int AtRiskTicketCount,
        int OutsideSlaOpenCount,
        int SlaRiskTicketCount,
        int MatchedCriteriaCount,
        IReadOnlyList<string> MatchedCriteria,
        int RulePriority,
        int RuleWeight);

    private sealed record SlotSkippedOwner(
        int RuleId,
        string? OwnerKey,
        int? UserId,
        string Reason,
        string Message);

    private sealed record SlotDecisionResult(
        string Slot,
        bool Applied,
        string Classification,
        string AppliedReason,
        SlotCandidateEvaluation? Selected,
        IReadOnlyList<SlotCandidateEvaluation> Candidates,
        IReadOnlyList<SlotSkippedOwner> SkippedReasons);
}
