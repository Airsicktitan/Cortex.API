using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class TicketRoutingRuleRepository(CortexDbContext context) : ITicketRoutingRuleRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<List<TicketRoutingRule>> GetAllAsync()
    {
        return await _context.TicketRoutingRules
            .OrderBy(rule => rule.Department)
            .ThenBy(rule => rule.Id)
            .ToListAsync();
    }

    public Task<TicketRoutingRule?> GetByIdAsync(int id)
    {
        return _context.TicketRoutingRules.FirstOrDefaultAsync(rule => rule.Id == id);
    }

    public async Task AddAsync(TicketRoutingRule rule)
    {
        await _context.TicketRoutingRules.AddAsync(rule);
    }

    public void Delete(TicketRoutingRule rule)
    {
        _context.TicketRoutingRules.Remove(rule);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
