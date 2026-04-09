using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class ArchiveConfigurationService(
    IArchiveConfigurationRepository repository,
    ITicketStatusService ticketStatusService) : IArchiveConfigurationService
{
    private readonly IArchiveConfigurationRepository _repository = repository;
    private readonly ITicketStatusService _ticketStatusService = ticketStatusService;

    public async Task<IReadOnlyList<ArchiveConfiguration>> GetAllAsync()
    {
        var configurations = await _repository.GetAllAsync();
        return configurations
            .Select(Clone)
            .ToList();
    }

    public async Task<ArchiveConfiguration> CreateAsync(ArchiveConfiguration configuration)
    {
        await ValidateAsync(configuration);

        var normalizedConfiguration = Clone(configuration);
        normalizedConfiguration.Id = 0;

        await _repository.AddAsync(normalizedConfiguration);
        await _repository.SaveChangesAsync();

        return Clone(normalizedConfiguration);
    }

    public async Task<ArchiveConfiguration> UpdateAsync(int id, ArchiveConfiguration configuration)
    {
        await ValidateAsync(configuration);

        var existingConfiguration = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Archive policy was not found.");

        existingConfiguration.ArchiveAfterDays = configuration.ArchiveAfterDays;
        existingConfiguration.EligibleStatuses = configuration.EligibleStatuses;

        await _repository.SaveChangesAsync();
        return Clone(existingConfiguration);
    }

    public async Task DeleteAsync(int id)
    {
        var existingConfiguration = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Archive policy was not found.");

        _repository.Delete(existingConfiguration);
        await _repository.SaveChangesAsync();
    }

    public IReadOnlyList<string> GetEligibleStatuses(ArchiveConfiguration configuration)
    {
        return configuration.EligibleStatuses;
    }

    public DateTime GetArchiveCutoffUtc(ArchiveConfiguration configuration, DateTime utcNow)
    {
        return utcNow.Date.AddDays(-configuration.ArchiveAfterDays);
    }

    private static ArchiveConfiguration Clone(ArchiveConfiguration configuration)
    {
        return new ArchiveConfiguration
        {
            Id = configuration.Id,
            ArchiveAfterDays = configuration.ArchiveAfterDays,
            EligibleStatuses = [.. configuration.EligibleStatuses]
        };
    }

    private async Task ValidateAsync(ArchiveConfiguration configuration)
    {
        if (configuration.ArchiveAfterDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Archive after days must be greater than zero.");
        }

        if (GetEligibleStatuses(configuration).Count == 0)
        {
            throw new ArgumentException("Select at least one archive status.", nameof(configuration));
        }

        var knownStatuses = await _ticketStatusService.GetKnownStatusNamesAsync();
        var unknownStatuses = GetEligibleStatuses(configuration)
            .Where(status => !knownStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (unknownStatuses.Count > 0)
        {
            throw new ArgumentException(
                $"These archive statuses are not registered: {string.Join(", ", unknownStatuses)}.",
                nameof(configuration));
        }
    }
}
