using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IArchiveConfigurationService
{
    Task<IReadOnlyList<ArchiveConfiguration>> GetAllAsync();
    Task<ArchiveConfiguration> CreateAsync(ArchiveConfiguration configuration);
    Task<ArchiveConfiguration> UpdateAsync(int id, ArchiveConfiguration configuration);
    Task DeleteAsync(int id);
    IReadOnlyList<string> GetEligibleStatuses(ArchiveConfiguration configuration);
    DateTime GetArchiveCutoffUtc(ArchiveConfiguration configuration, DateTime utcNow);
}
