using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class NotificationChannelConfigurationService(
    INotificationChannelConfigurationRepository repository)
    : INotificationChannelConfigurationService
{
    private readonly INotificationChannelConfigurationRepository _repository = repository;

    public async Task<NotificationChannelConfiguration> GetAsync()
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

    public async Task<NotificationChannelConfiguration> SaveAsync(
        NotificationChannelConfiguration configuration)
    {
        Validate(configuration);

        var normalizedConfiguration = Clone(configuration);

        await _repository.UpsertAsync(normalizedConfiguration);
        await _repository.SaveChangesAsync();

        var savedConfiguration = await _repository.GetAsync();
        return Clone(savedConfiguration ?? normalizedConfiguration);
    }

    private static NotificationChannelConfiguration GetDefaultConfiguration()
    {
        return new NotificationChannelConfiguration
        {
            AssignmentChannel = NotificationChannelMode.Neither,
            SlaRiskChannel = NotificationChannelMode.Neither
        };
    }

    private static NotificationChannelConfiguration Clone(NotificationChannelConfiguration configuration)
    {
        return new NotificationChannelConfiguration
        {
            Id = configuration.Id,
            AssignmentChannel = configuration.AssignmentChannel,
            SlaRiskChannel = configuration.SlaRiskChannel
        };
    }

    private static void Validate(NotificationChannelConfiguration configuration)
    {
        if (!Enum.IsDefined(configuration.AssignmentChannel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Assignment channel is not a valid notification channel.");
        }

        if (!Enum.IsDefined(configuration.SlaRiskChannel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "SLA-risk channel is not a valid notification channel.");
        }
    }
}
