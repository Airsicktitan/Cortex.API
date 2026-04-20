using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IAiSettingsService
{
    Task<AiSettingsConfiguration> GetAsync();
    Task<AiSettingsConfiguration> SaveAsync(AiSettingsConfiguration configuration);
}
