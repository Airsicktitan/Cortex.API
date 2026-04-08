using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ISlaConfigurationService
{
    Task<IReadOnlyList<SlaConfiguration>> GetAllAsync();
    Task<IReadOnlyDictionary<string, SlaConfiguration>> GetPriorityMapAsync();
    Task<IReadOnlyList<SlaConfiguration>> SaveAsync(IEnumerable<SlaConfiguration> configurations);
}
