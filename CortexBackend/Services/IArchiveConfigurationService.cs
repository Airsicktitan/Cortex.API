using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IArchiveConfigurationService
{
    Task<ArchiveConfiguration> GetAsync();
    Task<ArchiveConfiguration> SaveAsync(ArchiveConfiguration configuration);
    IReadOnlyList<string> GetEligibleStatuses(ArchiveConfiguration configuration);
    DateTime GetArchiveCutoffUtc(ArchiveConfiguration configuration, DateTime utcNow);
}
