using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class ArchiveConfigurationService(IArchiveConfigurationRepository repository) : IArchiveConfigurationService
{
    private readonly IArchiveConfigurationRepository _repository = repository;

    public async Task<ArchiveConfiguration> GetAsync()
    {
        var configuration = await _repository.GetAsync();
        if (configuration is not null)
        {
            return Clone(configuration);
        }

        var defaultConfiguration = GetDefaultConfiguration();
        await _repository.UpsertAsync(defaultConfiguration);
        await _repository.SaveChangesAsync();

        return Clone(defaultConfiguration);
    }

    public async Task<ArchiveConfiguration> SaveAsync(ArchiveConfiguration configuration)
    {
        Validate(configuration);

        var normalizedConfiguration = Clone(configuration);

        await _repository.UpsertAsync(normalizedConfiguration);
        await _repository.SaveChangesAsync();

        var savedConfiguration = await _repository.GetAsync();
        return Clone(savedConfiguration ?? normalizedConfiguration);
    }

    public IReadOnlyList<string> GetEligibleStatuses(ArchiveConfiguration configuration)
    {
        var statuses = new List<string>();

        if (configuration.ArchiveResolvedTickets)
        {
            statuses.Add("Resolved");
        }

        if (configuration.ArchiveClosedTickets)
        {
            statuses.Add("Closed");
        }

        return statuses;
    }

    public DateTime GetArchiveCutoffUtc(ArchiveConfiguration configuration, DateTime utcNow)
    {
        return utcNow.Date.AddDays(-configuration.ArchiveAfterDays);
    }

    private static ArchiveConfiguration GetDefaultConfiguration()
    {
        return new ArchiveConfiguration
        {
            ArchiveAfterDays = 30,
            ArchiveResolvedTickets = true,
            ArchiveClosedTickets = true
        };
    }

    private static ArchiveConfiguration Clone(ArchiveConfiguration configuration)
    {
        return new ArchiveConfiguration
        {
            Id = configuration.Id,
            ArchiveAfterDays = configuration.ArchiveAfterDays,
            ArchiveResolvedTickets = configuration.ArchiveResolvedTickets,
            ArchiveClosedTickets = configuration.ArchiveClosedTickets
        };
    }

    private void Validate(ArchiveConfiguration configuration)
    {
        if (configuration.ArchiveAfterDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Archive after days must be greater than zero.");
        }

        if (GetEligibleStatuses(configuration).Count == 0)
        {
            throw new ArgumentException("Select at least one archive status.", nameof(configuration));
        }
    }
}
