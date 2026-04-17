using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cortex.API.Services;

public class TicketRoutingRuleService(
    ITicketRoutingRuleRepository repository,
    CortexDbContext dbContext) : ITicketRoutingRuleService
{
    private readonly ITicketRoutingRuleRepository _repository = repository;
    private readonly CortexDbContext _dbContext = dbContext;
    private const string EngineVersion = "routing-engine-v1";

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

    public async Task<RoutingDecisionResult> EvaluateAsync(
        RoutingFactors factors,
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
            .OrderByDescending(match => match.Rule.RulePriority)
            .ThenByDescending(match => match.Rule.Weight)
            .ThenByDescending(match => match.MatchedCriteriaCount)
            .ThenBy(match => match.Rule.Id)
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

        var selected = candidateMatches[0];
        var confidence = selected.MatchedCriteriaCount >= 3
            ? RoutingConfidenceLevel.High
            : selected.MatchedCriteriaCount >= 2
                ? RoutingConfidenceLevel.Medium
                : RoutingConfidenceLevel.Low;
        var score = (selected.Rule.RulePriority * 10_000)
            + (selected.Rule.Weight * 100)
            + selected.MatchedCriteriaCount;
        var tieBreakKey = $"{selected.Rule.RulePriority:D6}|{selected.Rule.Weight:D6}|{selected.MatchedCriteriaCount:D2}|{selected.Rule.Id:D10}";

        var explanationObject = new
        {
            matchedRuleId = selected.Rule.Id,
            factors = normalizedFactors,
            matchedCriteria = selected.MatchedCriteria,
            rulePriority = selected.Rule.RulePriority,
            weight = selected.Rule.Weight,
            candidateCount = candidateMatches.Count
        };

        return new RoutingDecisionResult(
            MatchedRuleId: selected.Rule.Id,
            OutcomeType: RoutingOutcomeType.RuleMatch,
            ConfidenceLevel: confidence,
            NoMatchReason: null,
            RecommendedSynitiOwner: NormalizeOptionalValue(selected.Rule.SynitiOwner),
            RecommendedBusinessOwner: NormalizeOptionalValue(selected.Rule.BusinessOwner),
            PrecedenceScore: score,
            TieBreakKey: tieBreakKey,
            ExplanationJson: JsonSerializer.Serialize(explanationObject),
            ExplanationText: $"Matched routing rule #{selected.Rule.Id} using {string.Join(", ", selected.MatchedCriteria)}.",
            EngineVersion: EngineVersion,
            MatchedCriteriaCount: selected.MatchedCriteriaCount);
    }

    public async Task<TicketRoutingDecision> RecordDecisionAsync(
        string ticketId,
        RoutingDecisionResult decision,
        CancellationToken cancellationToken = default)
    {
        var entity = new TicketRoutingDecision
        {
            TicketId = ticketId,
            MatchedRuleId = decision.MatchedRuleId,
            OutcomeType = decision.OutcomeType,
            ConfidenceLevel = decision.ConfidenceLevel,
            NoMatchReason = decision.NoMatchReason,
            ChosenSynitiOwner = NormalizeOptionalValue(decision.RecommendedSynitiOwner),
            ChosenBusinessOwner = NormalizeOptionalValue(decision.RecommendedBusinessOwner),
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
        CancellationToken cancellationToken = default)
    {
        var entity = new TicketRoutingOverride
        {
            TicketId = ticketId,
            OverriddenByUserId = overriddenByUserId,
            PreviousSynitiOwner = NormalizeOptionalValue(previousSynitiOwner),
            PreviousBusinessOwner = NormalizeOptionalValue(previousBusinessOwner),
            NewSynitiOwner = NormalizeOptionalValue(newSynitiOwner),
            NewBusinessOwner = NormalizeOptionalValue(newBusinessOwner),
            OverrideReasonType = reasonType,
            OverrideReasonText = NormalizeOptionalValue(reasonText)
        };

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

    private static int GetMatchScore(
        TicketRoutingRule rule,
        string? normalizedDepartment,
        string? normalizedTitle)
    {
        var hasDepartmentCriterion = !string.IsNullOrWhiteSpace(rule.Department);
        var hasTitleCriterion = !string.IsNullOrWhiteSpace(rule.TitleContains);

        if (hasDepartmentCriterion)
        {
            if (normalizedDepartment is null)
            {
                return -1;
            }

            var ruleDepartment = NormalizeLookupValue(rule.Department);
            if (!string.Equals(ruleDepartment, normalizedDepartment, StringComparison.Ordinal))
            {
                return -1;
            }
        }

        if (hasTitleCriterion)
        {
            if (normalizedTitle is null)
            {
                return -1;
            }

            var titlePhrase = NormalizeLookupValue(rule.TitleContains);
            if (titlePhrase is null || !normalizedTitle.Contains(titlePhrase, StringComparison.Ordinal))
            {
                return -1;
            }
        }

        var score = 0;

        if (hasTitleCriterion)
        {
            score += 1_000 + (rule.TitleContains?.Trim().Length ?? 0);
        }

        if (hasDepartmentCriterion)
        {
            score += 100;
        }

        return score;
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

    private static RoutingDecisionResult BuildFallback(
        RoutingFactors factors,
        RoutingNoMatchReason noMatchReason,
        string text)
    {
        var explanationObject = new
        {
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

    private sealed record RuleMatchResult(
        TicketRoutingRule Rule,
        bool IsMatch,
        IReadOnlyList<string> MatchedCriteria)
    {
        public int MatchedCriteriaCount => MatchedCriteria.Count;
        public static RuleMatchResult NoMatch(TicketRoutingRule rule) => new(rule, false, []);
    }
}
