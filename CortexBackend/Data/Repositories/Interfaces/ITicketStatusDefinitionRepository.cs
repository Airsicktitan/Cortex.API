using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface ITicketStatusDefinitionRepository
{
    Task<List<TicketStatusDefinition>> GetAllAsync();
    Task<TicketStatusDefinition?> GetByIdAsync(int id);
    Task<TicketStatusDefinition?> GetByNameAsync(string name);
    Task AddAsync(TicketStatusDefinition definition);
    void Delete(TicketStatusDefinition definition);
    Task SaveChangesAsync();
}
