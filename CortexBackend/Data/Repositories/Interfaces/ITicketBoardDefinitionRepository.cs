using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface ITicketBoardDefinitionRepository
{
    Task<List<TicketBoardDefinition>> GetAllAsync();
    Task<TicketBoardDefinition?> GetByIdAsync(int id);
    Task<TicketBoardDefinition?> GetByNameAsync(string name);
    Task AddAsync(TicketBoardDefinition definition);
    Task<bool> IsBoardInUseAsync(int id);
    Task NormalizeBoardAssignmentsAsync(int defaultBoardId);
    void Delete(TicketBoardDefinition definition);
    Task SaveChangesAsync();
}
