using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed record TicketRoutingResolution(string? SynitiOwner, string? BusinessOwner);

public interface ITicketRoutingRuleService
{
    Task<IReadOnlyList<TicketRoutingRule>> GetAllAsync();
    Task<TicketRoutingRule> CreateAsync(TicketRoutingRule rule);
    Task<TicketRoutingRule> UpdateAsync(int id, TicketRoutingRule rule);
    Task DeleteAsync(int id);
    Task<TicketRoutingResolution> ResolveOwnersAsync(string? department, string? title);
}
