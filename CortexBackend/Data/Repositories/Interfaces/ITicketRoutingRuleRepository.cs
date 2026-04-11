using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface ITicketRoutingRuleRepository
{
    Task<List<TicketRoutingRule>> GetAllAsync();
    Task<TicketRoutingRule?> GetByIdAsync(int id);
    Task<TicketRoutingRule?> GetByDepartmentAsync(string department);
    Task AddAsync(TicketRoutingRule rule);
    void Delete(TicketRoutingRule rule);
    Task SaveChangesAsync();
}
