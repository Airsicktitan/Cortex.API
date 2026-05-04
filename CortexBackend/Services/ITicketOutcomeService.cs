using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Captures real ticket outcomes (initial assignment, overrides, terminal status, reopen)
/// into <see cref="TicketOutcome"/> rows. Advisory only; never mutates routing.
/// All writes are best-effort: callers never fail if outcome capture fails.
/// </summary>
public interface ITicketOutcomeService
{
    Task RecordInitialAssignmentAsync(
        Ticket ticket,
        int? matchedRuleId,
        CancellationToken cancellationToken = default);

    Task RecordOverrideAsync(
        string ticketId,
        string? finalSynitiOwner,
        string? finalBusinessOwner,
        CancellationToken cancellationToken = default);

    Task MarkReturnedForDetailAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    Task MarkReassignedAsync(
        Ticket ticket,
        string? previousSynitiOwner,
        CancellationToken cancellationToken = default);

    Task MarkSlaBreachedAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    Task MarkRoutingOverriddenAsync(
        string ticketId,
        string? finalSynitiOwner,
        string? finalBusinessOwner,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Ticket ticket,
        bool slaBreached,
        int commentCount,
        CancellationToken cancellationToken = default);

    Task RecordTerminalAsync(
        Ticket ticket,
        bool slaBreached,
        int commentCount,
        CancellationToken cancellationToken = default);

    Task RecordTerminalAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    Task RecordReopenAsync(
        string ticketId,
        CancellationToken cancellationToken = default);
}
