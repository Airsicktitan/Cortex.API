using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IArchiveConfigurationRepository
{
    Task<ArchiveConfiguration?> GetAsync();
    Task UpsertAsync(ArchiveConfiguration configuration);
    Task SaveChangesAsync();
}
