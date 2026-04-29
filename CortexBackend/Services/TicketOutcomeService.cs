using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <summary>
/// Persists ticket lifecycle outcomes to back the Cortex learning layer.
/// Idempotent upsert per ticket. Never blocks callers — failures are logged and swallowed.
/// </summary>
public sealed class TicketOutcomeService : ITicketOutcomeService
{
    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Resolved",
        "Closed",
        "Done",
        "Completed",
        "Cancelled",
        "Canceled",
    };

    private readonly CortexDbContext _db;
    private readonly ISlaConfigurationService _slaConfigurationService;
    private readonly ILogger<TicketOutcomeService> _logger;

    public TicketOutcomeService(
        CortexDbContext db,
        ISlaConfigurationService slaConfigurationService,
        ILogger<TicketOutcomeService> logger)
    {
        _db = db;
        _slaConfigurationService = slaConfigurationService;
        _logger = logger;
    }

    public static bool IsTerminalStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && TerminalStatuses.Contains(status.Trim());

    public async Task RecordInitialAssignmentAsync(
        Ticket ticket,
        int? matchedRuleId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket.Id))
        {
            return;
        }

        try
        {
            var (outcome, _) = await GetOrCreateOutcomeAsync(ticket.Id, cancellationToken);
            outcome.BoardId = ticket.BoardId;
            SetInitialOwnersIfMissing(outcome, ticket);
            SetFinalOwners(outcome, ticket.SynitiOwner, ticket.BusinessOwner);
            outcome.MatchedRuleId ??= matchedRuleId;
            Touch(outcome);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome initial assignment capture failed for ticket {TicketId}.",
                ticket.Id);
        }
    }

    public async Task RecordOverrideAsync(
        string ticketId,
        string? finalSynitiOwner,
        string? finalBusinessOwner,
        CancellationToken cancellationToken = default) =>
        await MarkRoutingOverriddenAsync(
            ticketId,
            finalSynitiOwner,
            finalBusinessOwner,
            cancellationToken);

    public async Task MarkReturnedForDetailAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket.Id))
        {
            return;
        }

        try
        {
            var (outcome, _) = await GetOrCreateOutcomeAsync(ticket.Id, cancellationToken);
            outcome.BoardId = ticket.BoardId;
            outcome.WasReturnedForDetail = true;
            SetInitialOwnersIfMissing(outcome, ticket);
            SetFinalOwners(outcome, ticket.SynitiOwner, ticket.BusinessOwner);
            Touch(outcome);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome return-for-detail capture failed for ticket {TicketId}.",
                ticket.Id);
        }
    }

    public async Task MarkReassignedAsync(
        Ticket ticket,
        string? previousSynitiOwner,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket.Id))
        {
            return;
        }

        try
        {
            var (outcome, _) = await GetOrCreateOutcomeAsync(ticket.Id, cancellationToken);
            var previousOwner = NormalizeOwner(previousSynitiOwner) ?? outcome.AssignedSynitiOwner;
            var currentOwner = NormalizeOwner(ticket.SynitiOwner);

            outcome.BoardId = ticket.BoardId;
            if (string.IsNullOrWhiteSpace(outcome.AssignedSynitiOwner))
            {
                outcome.AssignedSynitiOwner = previousOwner ?? currentOwner;
            }
            if (string.IsNullOrWhiteSpace(outcome.AssignedBusinessOwner))
            {
                outcome.AssignedBusinessOwner = NormalizeOwner(ticket.BusinessOwner);
            }

            if (HasMeaningfulOwnerChange(previousOwner, currentOwner)
                || HasMeaningfulOwnerChange(outcome.AssignedSynitiOwner, currentOwner))
            {
                outcome.WasReassigned = true;
            }

            SetFinalOwners(outcome, ticket.SynitiOwner, ticket.BusinessOwner);
            Touch(outcome);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome reassignment capture failed for ticket {TicketId}.",
                ticket.Id);
        }
    }

    public async Task MarkSlaBreachedAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket.Id))
        {
            return;
        }

        try
        {
            var (outcome, _) = await GetOrCreateOutcomeAsync(ticket.Id, cancellationToken);
            outcome.BoardId = ticket.BoardId;
            outcome.SlaBreached = true;
            outcome.WasSlaBreached = true;
            SetInitialOwnersIfMissing(outcome, ticket);
            SetFinalOwners(outcome, ticket.SynitiOwner, ticket.BusinessOwner);
            Touch(outcome);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome SLA breach capture failed for ticket {TicketId}.",
                ticket.Id);
        }
    }

    public async Task MarkRoutingOverriddenAsync(
        string ticketId,
        string? finalSynitiOwner,
        string? finalBusinessOwner,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return;
        }

        try
        {
            var (outcome, _) = await GetOrCreateOutcomeAsync(ticketId, cancellationToken);
            var normalizedSynitiOwner = NormalizeOwner(finalSynitiOwner);

            if ((outcome.WasRoutingOverridden
                    && HasMeaningfulOwnerChange(outcome.FinalSynitiOwner, normalizedSynitiOwner))
                || HasMeaningfulOwnerChange(outcome.AssignedSynitiOwner, normalizedSynitiOwner))
            {
                outcome.WasReassigned = true;
            }

            outcome.WasOverridden = true;
            outcome.WasRoutingOverridden = true;
            SetFinalOwners(outcome, finalSynitiOwner, finalBusinessOwner);
            Touch(outcome);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome override capture failed for ticket {TicketId}.",
                ticketId);
        }
    }

    public async Task MarkCompletedAsync(
        Ticket ticket,
        bool slaBreached,
        int commentCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket.Id))
        {
            return;
        }

        try
        {
            var resolvedCommentCount = commentCount >= 0
                ? commentCount
                : await _db.Comments
                    .AsNoTracking()
                    .CountAsync(c => c.TicketId == ticket.Id, cancellationToken);

            var (outcome, _) = await GetOrCreateOutcomeAsync(ticket.Id, cancellationToken);
            outcome.BoardId = ticket.BoardId;
            SetInitialOwnersIfMissing(outcome, ticket);
            SetFinalOwners(outcome, ticket.SynitiOwner, ticket.BusinessOwner);
            outcome.SlaBreached = slaBreached;
            outcome.WasSlaBreached = slaBreached;
            outcome.CommentCount = resolvedCommentCount;
            outcome.ReachedTerminalStatus = true;
            outcome.CompletedAtUtc = DateTime.UtcNow;
            outcome.CompletedAt = outcome.CompletedAtUtc;
            Touch(outcome);

            if (HasMeaningfulOwnerChange(outcome.AssignedSynitiOwner, outcome.FinalSynitiOwner))
            {
                outcome.WasReassigned = true;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome completion capture failed for ticket {TicketId}.",
                ticket.Id);
        }
    }

    public async Task RecordTerminalAsync(
        Ticket ticket,
        bool slaBreached,
        int commentCount,
        CancellationToken cancellationToken = default) =>
        await MarkCompletedAsync(ticket, slaBreached, commentCount, cancellationToken);

    /// <summary>
    /// Convenience overload that computes SLA breach and comment count from current state.
    /// </summary>
    public async Task RecordTerminalAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var slaConfigurations = await _slaConfigurationService.GetPriorityMapAsync();
            slaConfigurations.TryGetValue(ticket.Priority ?? "Medium", out var configuration);
            var snapshot = TicketSlaCalculator.Calculate(ticket, configuration);
            await RecordTerminalAsync(ticket, snapshot.IsBreached, -1, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome terminal auto-capture failed for ticket {TicketId}.",
                ticket.Id);
        }
    }

    public async Task RecordReopenAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return;
        }

        try
        {
            var outcome = await _db.TicketOutcomes
                .FirstOrDefaultAsync(o => o.TicketId == ticketId, cancellationToken);

            if (outcome is null || !outcome.ReachedTerminalStatus)
            {
                return;
            }

            outcome.WasReopened = true;
            outcome.ReachedTerminalStatus = false;
            Touch(outcome);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome reopen capture failed for ticket {TicketId}.",
                ticketId);
        }
    }

    private async Task<(TicketOutcome Outcome, bool Created)> GetOrCreateOutcomeAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        var outcome = await _db.TicketOutcomes
            .FirstOrDefaultAsync(o => o.TicketId == ticketId, cancellationToken);

        if (outcome is not null)
        {
            return (outcome, false);
        }

        outcome = new TicketOutcome
        {
            TicketId = ticketId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.TicketOutcomes.Add(outcome);
        return (outcome, true);
    }

    private static void SetInitialOwnersIfMissing(TicketOutcome outcome, Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(outcome.AssignedSynitiOwner))
        {
            outcome.AssignedSynitiOwner = NormalizeOwner(ticket.SynitiOwner);
        }
        if (string.IsNullOrWhiteSpace(outcome.AssignedBusinessOwner))
        {
            outcome.AssignedBusinessOwner = NormalizeOwner(ticket.BusinessOwner);
        }
    }

    private static void SetFinalOwners(
        TicketOutcome outcome,
        string? synitiOwner,
        string? businessOwner)
    {
        outcome.FinalSynitiOwner = NormalizeOwner(synitiOwner);
        outcome.FinalBusinessOwner = NormalizeOwner(businessOwner);
        outcome.FinalOwner = outcome.FinalSynitiOwner;
    }

    private static void Touch(TicketOutcome outcome)
    {
        outcome.LastUpdatedAtUtc = DateTime.UtcNow;
        outcome.LastUpdatedAt = outcome.LastUpdatedAtUtc;
    }

    private static bool HasMeaningfulOwnerChange(string? previousOwner, string? currentOwner)
    {
        var previous = NormalizeOwner(previousOwner);
        var current = NormalizeOwner(currentOwner);

        return previous is not null
            && current is not null
            && !string.Equals(previous, current, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOwner(string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return null;
        }

        return owner.Trim();
    }
}
