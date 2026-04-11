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
        existingRule.SynitiOwner = normalizedRule.SynitiOwner;
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

    public async Task<string?> ResolveSynitiOwnerAsync(string? department)
    {
        if (string.IsNullOrWhiteSpace(department))
        {
            return null;
        }

        var rule = await _repository.GetByDepartmentAsync(department);
        return rule is { IsEnabled: true } ? rule.SynitiOwner : null;
    }

    private async Task ValidateAsync(TicketRoutingRule rule, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(rule.Department))
        {
            throw new ArgumentException("Department is required.", nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(rule.SynitiOwner))
        {
            throw new ArgumentException("Syniti owner is required.", nameof(rule));
        }

        var duplicateRule = await _repository.GetByDepartmentAsync(rule.Department);
        if (duplicateRule is not null && duplicateRule.Id != existingId)
        {
            throw new ArgumentException(
                "A routing rule for this department already exists.",
                nameof(rule));
        }
    }

    private static TicketRoutingRule Normalize(TicketRoutingRule rule)
    {
        return new TicketRoutingRule
        {
            Department = rule.Department.Trim(),
            SynitiOwner = rule.SynitiOwner.Trim(),
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
            SynitiOwner = rule.SynitiOwner,
            IsEnabled = rule.IsEnabled,
            CreatedDateUtc = rule.CreatedDateUtc,
            LastModifiedDateUtc = rule.LastModifiedDateUtc
        };
    }
}
