using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface ISlaConfigurationRepository
{
    Task<IReadOnlyList<SlaConfiguration>> GetAllAsync();
    Task UpsertRangeAsync(IEnumerable<SlaConfiguration> configurations);
    Task SaveChangesAsync();
}
