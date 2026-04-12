using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class TicketRoutingRuleService(
    ITicketRoutingRuleRepository repository) : ITicketRoutingRuleService
{
    private readonly ITicketRoutingRuleRepository _repository = repository;

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
        var normalizedDepartment = NormalizeLookupValue(department);
        var normalizedTitle = NormalizeLookupValue(title);
        var matchedRule = (await _repository.GetAllAsync())
            .Where(rule => rule.IsEnabled)
            .Select(rule => new
            {
                Rule = rule,
                Score = GetMatchScore(rule, normalizedDepartment, normalizedTitle)
            })
            .Where(entry => entry.Score >= 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Rule.Id)
            .Select(entry => entry.Rule)
            .FirstOrDefault();

        return matchedRule is null
            ? new TicketRoutingResolution(null, null)
            : new TicketRoutingResolution(
                NormalizeOptionalValue(matchedRule.SynitiOwner),
                NormalizeOptionalValue(matchedRule.BusinessOwner));
    }

    private async Task ValidateAsync(TicketRoutingRule rule, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(rule.Department)
            && string.IsNullOrWhiteSpace(rule.TitleContains))
        {
            throw new ArgumentException(
                "Add a routing department, a title match phrase, or both.",
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
                "A routing rule with the same department and title match already exists.",
                nameof(rule));
        }
    }

    private static TicketRoutingRule Normalize(TicketRoutingRule rule)
    {
        return new TicketRoutingRule
        {
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
}
