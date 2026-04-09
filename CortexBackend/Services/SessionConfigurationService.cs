using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class SessionConfigurationService(ISessionConfigurationRepository repository) : ISessionConfigurationService
{
    private readonly ISessionConfigurationRepository _repository = repository;

    public async Task<SessionConfiguration> GetAsync()
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

    public async Task<SessionConfiguration> SaveAsync(SessionConfiguration configuration)
    {
        Validate(configuration);

        var normalizedConfiguration = Clone(configuration);

        await _repository.UpsertAsync(normalizedConfiguration);
        await _repository.SaveChangesAsync();

        var savedConfiguration = await _repository.GetAsync();
        return Clone(savedConfiguration ?? normalizedConfiguration);
    }

    private static SessionConfiguration GetDefaultConfiguration()
    {
        return new SessionConfiguration
        {
            InactivityTimeoutMinutes = 10,
            WarningMinutes = 1
        };
    }

    private static SessionConfiguration Clone(SessionConfiguration configuration)
    {
        return new SessionConfiguration
        {
            Id = configuration.Id,
            InactivityTimeoutMinutes = configuration.InactivityTimeoutMinutes,
            WarningMinutes = configuration.WarningMinutes
        };
    }

    private static void Validate(SessionConfiguration configuration)
    {
        if (configuration.InactivityTimeoutMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Inactivity timeout must be greater than zero minutes.");
        }

        if (configuration.WarningMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Warning window cannot be negative.");
        }

        if (configuration.WarningMinutes >= configuration.InactivityTimeoutMinutes)
        {
            throw new ArgumentException(
                "Warning window must be shorter than the inactivity timeout.",
                nameof(configuration));
        }
    }
}
