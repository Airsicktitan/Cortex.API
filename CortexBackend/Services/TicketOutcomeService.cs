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
            var outcome = await _db.TicketOutcomes
                .FirstOrDefaultAsync(o => o.TicketId == ticket.Id, cancellationToken);

            if (outcome is null)
            {
                outcome = new TicketOutcome
                {
                    TicketId = ticket.Id,
                    BoardId = ticket.BoardId,
                    AssignedSynitiOwner = NormalizeOwner(ticket.SynitiOwner),
                    AssignedBusinessOwner = NormalizeOwner(ticket.BusinessOwner),
                    FinalSynitiOwner = NormalizeOwner(ticket.SynitiOwner),
                    FinalBusinessOwner = NormalizeOwner(ticket.BusinessOwner),
                    MatchedRuleId = matchedRuleId,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                _db.TicketOutcomes.Add(outcome);
            }
            else
            {
                outcome.BoardId = ticket.BoardId;
                if (string.IsNullOrWhiteSpace(outcome.AssignedSynitiOwner))
                {
                    outcome.AssignedSynitiOwner = NormalizeOwner(ticket.SynitiOwner);
                }
                if (string.IsNullOrWhiteSpace(outcome.AssignedBusinessOwner))
                {
                    outcome.AssignedBusinessOwner = NormalizeOwner(ticket.BusinessOwner);
                }
                outcome.FinalSynitiOwner = NormalizeOwner(ticket.SynitiOwner);
                outcome.FinalBusinessOwner = NormalizeOwner(ticket.BusinessOwner);
                outcome.MatchedRuleId ??= matchedRuleId;
                outcome.LastUpdatedAtUtc = DateTime.UtcNow;
            }

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

            if (outcome is null)
            {
                outcome = new TicketOutcome
                {
                    TicketId = ticketId,
                    FinalSynitiOwner = NormalizeOwner(finalSynitiOwner),
                    FinalBusinessOwner = NormalizeOwner(finalBusinessOwner),
                    WasOverridden = true,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                _db.TicketOutcomes.Add(outcome);
            }
            else
            {
                if (outcome.WasOverridden)
                {
                    outcome.WasReassigned = true;
                }
                outcome.WasOverridden = true;
                outcome.FinalSynitiOwner = NormalizeOwner(finalSynitiOwner);
                outcome.FinalBusinessOwner = NormalizeOwner(finalBusinessOwner);
                outcome.LastUpdatedAtUtc = DateTime.UtcNow;
            }

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

    public async Task RecordTerminalAsync(
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
            // If commentCount was not provided (negative sentinel), look it up.
            var resolvedCommentCount = commentCount >= 0
                ? commentCount
                : await _db.Comments
                    .AsNoTracking()
                    .CountAsync(c => c.TicketId == ticket.Id, cancellationToken);

            var outcome = await _db.TicketOutcomes
                .FirstOrDefaultAsync(o => o.TicketId == ticket.Id, cancellationToken);

            if (outcome is null)
            {
                outcome = new TicketOutcome
                {
                    TicketId = ticket.Id,
                    BoardId = ticket.BoardId,
                    AssignedSynitiOwner = NormalizeOwner(ticket.SynitiOwner),
                    AssignedBusinessOwner = NormalizeOwner(ticket.BusinessOwner),
                    CreatedAtUtc = DateTime.UtcNow,
                };
                _db.TicketOutcomes.Add(outcome);
            }

            outcome.BoardId = ticket.BoardId;
            outcome.FinalSynitiOwner = NormalizeOwner(ticket.SynitiOwner);
            outcome.FinalBusinessOwner = NormalizeOwner(ticket.BusinessOwner);
            outcome.SlaBreached = slaBreached;
            outcome.CommentCount = resolvedCommentCount;
            outcome.ReachedTerminalStatus = true;
            outcome.CompletedAtUtc = DateTime.UtcNow;
            outcome.LastUpdatedAtUtc = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(outcome.AssignedSynitiOwner)
                && !string.Equals(
                    outcome.AssignedSynitiOwner?.Trim(),
                    outcome.FinalSynitiOwner?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                outcome.WasReassigned = true;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TicketOutcome terminal capture failed for ticket {TicketId}.",
                ticket.Id);
        }
    }

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
            outcome.LastUpdatedAtUtc = DateTime.UtcNow;

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

    private static string? NormalizeOwner(string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return null;
        }

        return owner.Trim();
    }
}
