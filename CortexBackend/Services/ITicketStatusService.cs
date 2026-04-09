using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ITicketStatusService
{
    Task<IReadOnlyList<TicketStatusDefinition>> GetAllAsync();
    Task<IReadOnlyList<TicketStatusDefinition>> GetEnabledAsync();
    Task<TicketStatusDefinition> CreateAsync(TicketStatusDefinition definition);
    Task<TicketStatusDefinition> UpdateAsync(int id, TicketStatusDefinition definition);
    Task DeleteAsync(int id);
    Task EnsureSelectableStatusAsync(string statusName);
    Task<string> GetDefaultCreateStatusAsync();
    Task<string> GetReactivatedStatusAsync(string archivedStatus);
    Task<IReadOnlyCollection<string>> GetKnownStatusNamesAsync();
}
