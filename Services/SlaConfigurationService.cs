using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class SlaConfigurationService(ISlaConfigurationRepository repository) : ISlaConfigurationService
{
    private static readonly string[] PriorityOrder = ["Critical", "High", "Medium", "Low"];
    private readonly ISlaConfigurationRepository _repository = repository;

    public async Task<IReadOnlyList<SlaConfiguration>> GetAllAsync()
    {
        var configurations = await _repository.GetAllAsync();
        if (configurations.Count > 0)
        {
            return Order(configurations);
        }

        var defaults = TicketSlaCalculator.GetDefaultPolicies()
            .Select(Clone)
            .ToList();

        await _repository.UpsertRangeAsync(defaults);
        await _repository.SaveChangesAsync();

        return defaults;
    }

    public async Task<IReadOnlyDictionary<string, SlaConfiguration>> GetPriorityMapAsync()
    {
        var configurations = await GetAllAsync();

        return configurations.ToDictionary(
            configuration => configuration.Priority,
            Clone,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<SlaConfiguration>> SaveAsync(IEnumerable<SlaConfiguration> configurations)
    {
        var normalizedConfigurations = TicketSlaCalculator.GetDefaultPolicies()
            .Select(Clone)
            .ToDictionary(configuration => configuration.Priority, StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in configurations)
        {
            var priority = NormalizePriority(configuration.Priority);

            if (configuration.TargetHours <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configurations), $"{priority} target hours must be greater than zero.");
            }

            if (configuration.WarningHours < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configurations), $"{priority} warning hours cannot be negative.");
            }

            if (configuration.WarningHours >= configuration.TargetHours)
            {
                throw new ArgumentOutOfRangeException(nameof(configurations), $"{priority} warning hours must be less than the target hours.");
            }

            normalizedConfigurations[priority] = new SlaConfiguration
            {
                Priority = priority,
                TargetHours = configuration.TargetHours,
                WarningHours = configuration.WarningHours
            };
        }

        var orderedConfigurations = Order(normalizedConfigurations.Values);

        await _repository.UpsertRangeAsync(orderedConfigurations);
        await _repository.SaveChangesAsync();

        return orderedConfigurations;
    }

    private static string NormalizePriority(string? priority)
    {
        var normalizedPriority = PriorityOrder.FirstOrDefault(candidate =>
            candidate.Equals(priority?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (normalizedPriority is null)
        {
            throw new ArgumentException($"Unsupported SLA priority '{priority}'.", nameof(priority));
        }

        return normalizedPriority;
    }

    private static SlaConfiguration Clone(SlaConfiguration configuration)
    {
        return new SlaConfiguration
        {
            Priority = configuration.Priority,
            TargetHours = configuration.TargetHours,
            WarningHours = configuration.WarningHours
        };
    }

    private static IReadOnlyList<SlaConfiguration> Order(IEnumerable<SlaConfiguration> configurations)
    {
        return configurations
            .OrderBy(configuration => Array.IndexOf(PriorityOrder, configuration.Priority))
            .Select(Clone)
            .ToList();
    }
}
