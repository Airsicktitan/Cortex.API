using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface ISessionConfigurationRepository
{
    Task<SessionConfiguration?> GetAsync();
    Task UpsertAsync(SessionConfiguration configuration);
    Task SaveChangesAsync();
}
