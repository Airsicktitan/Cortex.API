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
    Task<RoutingDecisionResult> EvaluateAsync(RoutingFactors factors, CancellationToken cancellationToken = default);
    Task<TicketRoutingDecision> RecordDecisionAsync(string ticketId, RoutingDecisionResult decision, CancellationToken cancellationToken = default);
    Task<TicketRoutingOverride> RecordOverrideAsync(
        string ticketId,
        int overriddenByUserId,
        string? previousSynitiOwner,
        string? previousBusinessOwner,
        string? newSynitiOwner,
        string? newBusinessOwner,
        RoutingOverrideReasonType reasonType,
        string? reasonText,
        CancellationToken cancellationToken = default);
    Task<TicketRoutingDecision?> GetLatestDecisionAsync(string ticketId, CancellationToken cancellationToken = default);
    Task<TicketRoutingOverride?> GetLatestOverrideAsync(string ticketId, CancellationToken cancellationToken = default);
}
