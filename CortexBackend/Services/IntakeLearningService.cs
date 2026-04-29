using Cortex.API.Database;
using Cortex.API.DTO;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class IntakeLearningService(CortexDbContext db) : IIntakeLearningService
{
    private static readonly IReadOnlyList<string> StandardLimitations =
    [
        "Returned-for-detail is durable on the ticket outcome, but free-text return reasons may be cleared after resubmission or approval.",
        "Missing-detail hints reflect the latest persisted AI triage snapshot on the ticket, not guaranteed state at return time.",
        "Missing-detail hints are free-text; treat counts as friction indicators, not categorical root-cause proof.",
        "Requester department is taken from current user profile data and may be missing (Unknown).",
        "Rates show correlation with follow-up friction patterns, not proof of causal relationships.",
    ];

    public async Task<IntakeLearningOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var boards = await db.TicketBoardDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        var rows = await (
            from t in db.Tickets.AsNoTracking()
            join o in db.TicketOutcomes.AsNoTracking() on t.Id equals o.TicketId
            join u in db.Users.AsNoTracking() on t.CreatedBy equals u.Id into uJoin
            from u in uJoin.DefaultIfEmpty()
            select new IntakeLearningRowProjection(
                t.BoardId,
                t.Priority ?? string.Empty,
                u != null ? u.Department : null,
                t.ReturnReason,
                t.AiTriageMissingDetailsJson,
                o.WasReturnedForDetail)).ToListAsync(cancellationToken);

        var unknownDeptCount = rows.Count(IsUnknownDepartment);

        var boardReturns = rows
            .GroupBy(r => r.BoardId)
            .Select(g =>
            {
                var total = g.Count();
                var returned = g.Count(x => x.WasReturnedForDetail);
                return new IntakeLearningGroupDto
                {
                    Key = $"board-{g.Key}",
                    Label = ResolveBoardLabel(g.Key, boards),
                    TotalTickets = total,
                    ReturnedTickets = returned,
                    ReturnRatePercent = SafePercent(returned, total),
                };
            })
            .OrderByDescending(x => x.ReturnRatePercent)
            .ThenBy(x => x.Label)
            .ToList();

        var priorityReturns = rows
            .GroupBy(r => NormalizePriority(r.Priority))
            .Select(g =>
            {
                var total = g.Count();
                var returned = g.Count(x => x.WasReturnedForDetail);
                return new IntakeLearningGroupDto
                {
                    Key = $"priority:{g.Key}",
                    Label = g.Key,
                    TotalTickets = total,
                    ReturnedTickets = returned,
                    ReturnRatePercent = SafePercent(returned, total),
                };
            })
            .OrderByDescending(x => x.ReturnRatePercent)
            .ThenBy(x => x.Label)
            .ToList();

        var departmentReturns = rows
            .GroupBy(r => DepartmentKey(r.Department))
            .Select(g =>
            {
                var total = g.Count();
                var returned = g.Count(x => x.WasReturnedForDetail);
                return new IntakeLearningGroupDto
                {
                    Key = g.Key,
                    Label = g.Key == "unknown" ? "Unknown" : g.Key,
                    TotalTickets = total,
                    ReturnedTickets = returned,
                    ReturnRatePercent = SafePercent(returned, total),
                };
            })
            .OrderByDescending(x => x.ReturnRatePercent)
            .ThenBy(x => x.Label)
            .ToList();

        var returnedRows = rows.Where(r => r.WasReturnedForDetail).ToList();
        var returnedTicketCount = returnedRows.Count;

        var stillHasReason = returnedRows.Count(r =>
            r.ReturnReason is not null && r.ReturnReason.Trim().Length > 0);

        var reasonPercent = SafePercent(stillHasReason, returnedTicketCount);

        var hintCounts = returnedRows
            .Select(r => IntakeLearningMissingHintCounter.CountMissingHints(r.AiTriageMissingDetailsJson))
            .ToList();

        var withHints = hintCounts.Count(n => n > 0);
        var avgHints = returnedTicketCount == 0
            ? 0d
            : Math.Round(hintCounts.Average(), 2);

        var zero = hintCounts.Count(n => n == 0);
        var oneToTwo = hintCounts.Count(n => n is >= 1 and <= 2);
        var threeToFive = hintCounts.Count(n => n is >= 3 and <= 5);
        var sixPlus = hintCounts.Count(n => n >= 6);

        return new IntakeLearningOverviewDto
        {
            BoardReturns = boardReturns,
            PriorityReturns = priorityReturns,
            DepartmentReturns = departmentReturns,
            UnknownDepartmentTicketCount = unknownDeptCount,
            ReturnReasonAvailability = new ReturnReasonAvailabilityDto
            {
                ReturnedTickets = returnedTicketCount,
                ReturnReasonStillAvailableCount = stillHasReason,
                ReturnReasonAvailabilityPercent = reasonPercent,
            },
            MissingHintSummary = new MissingHintSummaryDto
            {
                ReturnedTickets = returnedTicketCount,
                ReturnedTicketsWithMissingHintJson = withHints,
                AverageMissingHintCount = avgHints,
                ZeroHintsCount = zero,
                OneToTwoHintsCount = oneToTwo,
                ThreeToFiveHintsCount = threeToFive,
                SixPlusHintsCount = sixPlus,
            },
            GeneratedAtUtc = DateTime.UtcNow,
            Limitations = StandardLimitations,
        };
    }

    private static bool IsUnknownDepartment(IntakeLearningRowProjection r) =>
        string.IsNullOrWhiteSpace(r.Department);

    private static string NormalizePriority(string priority) =>
        string.IsNullOrWhiteSpace(priority) ? "(Unset)" : priority.Trim();

    private static string DepartmentKey(string? department)
    {
        if (string.IsNullOrWhiteSpace(department))
        {
            return "unknown";
        }

        return department.Trim();
    }

    private static string ResolveBoardLabel(int boardId, IReadOnlyDictionary<int, string> boards) =>
        boards.TryGetValue(boardId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : $"Board {boardId}";

    private static double SafePercent(int numerator, int denominator) =>
        denominator == 0 ? 0 : Math.Round(100.0 * numerator / denominator, 2);

    private sealed record IntakeLearningRowProjection(
        int BoardId,
        string Priority,
        string? Department,
        string? ReturnReason,
        string? AiTriageMissingDetailsJson,
        bool WasReturnedForDetail);
}
