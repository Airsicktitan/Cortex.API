using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IArchiveConfigurationRepository
{
    Task<IReadOnlyList<ArchiveConfiguration>> GetAllAsync();
    Task<ArchiveConfiguration?> GetByIdAsync(int id);
    Task AddAsync(ArchiveConfiguration configuration);
    void Delete(ArchiveConfiguration configuration);
    Task SaveChangesAsync();
}
