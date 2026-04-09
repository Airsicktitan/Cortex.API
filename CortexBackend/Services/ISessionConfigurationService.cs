using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ISessionConfigurationService
{
    Task<SessionConfiguration> GetAsync();
    Task<SessionConfiguration> SaveAsync(SessionConfiguration configuration);
}
