using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ITicketBoardService
{
    Task<IReadOnlyList<TicketBoardDefinition>> GetAllAsync();
    Task<IReadOnlyList<TicketBoardDefinition>> GetEnabledAsync();
    Task<TicketBoardDefinition> CreateAsync(TicketBoardDefinition definition);
    Task<TicketBoardDefinition> UpdateAsync(int id, TicketBoardDefinition definition);
    Task DeleteAsync(int id);
    Task<TicketBoardDefinition> GetDefaultCreateBoardAsync();
    Task<TicketBoardDefinition?> GetByIdAsync(int id);
}
