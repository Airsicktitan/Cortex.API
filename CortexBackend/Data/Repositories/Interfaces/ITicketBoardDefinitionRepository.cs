using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface ITicketBoardDefinitionRepository
{
    Task<List<TicketBoardDefinition>> GetAllAsync();
    Task<TicketBoardDefinition?> GetByIdAsync(int id);
    Task AddAsync(TicketBoardDefinition definition);
    Task<bool> IsBoardInUseAsync(int id);
    void Delete(TicketBoardDefinition definition);
    Task SaveChangesAsync();
}
