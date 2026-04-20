using System.Globalization;
using System.Text;
using Cortex.API.Database;
using Cortex.API.DTO;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <summary>
/// v1 recurring-issue detector.
/// Approach is intentionally explainable:
///   1. Normalize titles (lowercase, strip punctuation, drop stopwords + short tokens).
///   2. Pick the top 3 signature tokens (alphabetical for stability).
///   3. Group by (BoardId, signature). Groups with count &gt;= <see cref="MinimumGroupSize"/> are recurring.
///   4. Compute honest metrics — resolution time is derived from CreatedDate vs close time
///      (LastModifiedDate for terminal statuses on live tickets, ArchivedDate for archived),
///      which is a duration proxy and NOT human work time. Language in DTOs reflects that.
/// </summary>
public sealed class RepeatIssueAnalyticsService : IRepeatIssueAnalyticsService
{
    internal const int MinimumGroupSize = 3;
    internal const int MaxSignatureTokens = 3;
    internal const int MinTokenLength = 4;

    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Resolved",
        "Closed",
        "Done",
        "Completed",
        "Cancelled",
        "Canceled",
        "Rejected",
        "Archived",
    };

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "into", "onto", "that", "this", "these", "those",
        "about", "after", "before", "between", "during", "over", "under", "through",
        "please", "help", "need", "needs", "want", "wants", "would", "could", "should",
        "ticket", "tickets", "issue", "issues", "problem", "problems", "request", "requests",
        "error", "errors", "bug", "bugs", "fix", "fixing", "update", "updating",
        "new", "add", "adding", "remove", "removing", "change", "changes", "changing",
        "when", "where", "what", "which", "while", "since", "because",
        "user", "users", "team", "teams", "test", "testing", "tests",
    };

    private readonly CortexDbContext _db;

    public RepeatIssueAnalyticsService(CortexDbContext db)
    {
        _db = db;
    }

    public async Task<RepeatIssueOverviewResponse> GetOverviewAsync(
        int topN,
        CancellationToken cancellationToken = default)
    {
        var safeTopN = Math.Clamp(topN, 1, 50);
        var groups = await BuildGroupsAsync(cancellationToken);

        var ranked = groups
            .OrderByDescending(group => group.RepeatCount)
            .ThenByDescending(group => group.LastSeenUtc)
            .ToList();

        var headline = new RepeatIssueOverviewResponse
        {
            TotalRecurringGroups = ranked.Count,
            TicketsInRecurringGroups = ranked.Sum(group => group.RepeatCount),
            OpenTicketsInRecurringGroups = ranked.Sum(group => group.OpenCount),
            TotalResolutionHoursInRecurringGroups =
                Math.Round(ranked.Sum(group => group.TotalResolutionHours), 1),
            MinimumGroupSize = MinimumGroupSize,
            GeneratedUtc = DateTime.UtcNow,
            Groups = ranked.Take(safeTopN).Select(ToSummary).ToList(),
        };

        return headline;
    }

    public async Task<RepeatIssueGroupDetailResponse?> GetGroupDetailAsync(
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            return null;
        }

        var groups = await BuildGroupsAsync(cancellationToken);
        var match = groups.FirstOrDefault(group =>
            string.Equals(group.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }

        var tickets = match.Tickets
            .OrderByDescending(ticket => ticket.CreatedDate)
            .Select(ticket => new RepeatIssueTicketSummary
            {
                TicketId = ticket.TicketId,
                Title = ticket.Title,
                Priority = ticket.Priority,
                Status = ticket.Status,
                IsArchived = ticket.IsArchived,
                CreatedDate = ticket.CreatedDate,
                ClosedDate = ticket.ClosedDate,
                ResolutionHours = ticket.ResolutionHours is null
                    ? null
                    : Math.Round(ticket.ResolutionHours.Value, 1),
                CommentCount = ticket.CommentCount,
                Owner = ticket.Owner,
            })
            .ToList();

        var boards = match.Tickets
            .Select(ticket => ticket.BoardName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        var owners = match.Tickets
            .Select(ticket => ticket.Owner)
            .Where(owner => !string.IsNullOrWhiteSpace(owner))
            .Select(owner => owner!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(owner => owner)
            .ToList();

        var dominantPriority = match.Tickets
            .Where(ticket => !string.IsNullOrWhiteSpace(ticket.Priority))
            .GroupBy(ticket => ticket.Priority, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(grouping => grouping.Count())
            .ThenBy(grouping => grouping.Key)
            .Select(grouping => grouping.Key)
            .FirstOrDefault();

        var dominantStatus = match.Tickets
            .Where(ticket => !string.IsNullOrWhiteSpace(ticket.Status))
            .GroupBy(ticket => ticket.Status, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(grouping => grouping.Count())
            .ThenBy(grouping => grouping.Key)
            .Select(grouping => grouping.Key)
            .FirstOrDefault();

        return new RepeatIssueGroupDetailResponse
        {
            Summary = ToSummary(match),
            Boards = boards,
            Owners = owners,
            DominantPriority = dominantPriority,
            DominantStatus = dominantStatus,
            Tickets = tickets,
        };
    }

    private async Task<List<GroupSnapshot>> BuildGroupsAsync(CancellationToken cancellationToken)
    {
        var boardNames = await _db.TicketBoardDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(board => board.Id, board => board.Name, cancellationToken);

        var liveTickets = await _db.Tickets
            .AsNoTracking()
            .Select(ticket => new TicketSnapshot
            {
                TicketId = ticket.Id,
                Title = ticket.Title,
                Priority = ticket.Priority,
                Status = ticket.Status,
                BoardId = ticket.BoardId,
                CreatedDate = ticket.CreatedDate,
                LastModifiedDate = ticket.LastModifiedDate,
                SynitiOwner = ticket.SynitiOwner,
                BusinessOwner = ticket.BusinessOwner,
                IsArchived = false,
                ArchivedDate = null,
                ArchivedCommentCount = 0,
            })
            .ToListAsync(cancellationToken);

        var liveIds = liveTickets.Select(ticket => ticket.TicketId).ToList();
        var commentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (liveIds.Count > 0)
        {
            var counts = await _db.Comments
                .AsNoTracking()
                .Where(comment => liveIds.Contains(comment.TicketId))
                .GroupBy(comment => comment.TicketId)
                .Select(grouping => new { TicketId = grouping.Key, Count = grouping.Count() })
                .ToListAsync(cancellationToken);
            foreach (var pair in counts)
            {
                commentCounts[pair.TicketId] = pair.Count;
            }
        }

        var archivedTickets = await _db.ArchivedTickets
            .AsNoTracking()
            .Select(ticket => new TicketSnapshot
            {
                TicketId = ticket.Id,
                Title = ticket.Title,
                Priority = ticket.Priority,
                Status = ticket.Status,
                BoardId = ticket.BoardId,
                CreatedDate = ticket.CreatedDate,
                LastModifiedDate = ticket.LastModifiedDate,
                SynitiOwner = ticket.SynitiOwner,
                BusinessOwner = ticket.BusinessOwner,
                IsArchived = true,
                ArchivedDate = ticket.ArchivedDate,
                ArchivedCommentCount = ticket.CommentCount,
            })
            .ToListAsync(cancellationToken);

        var all = new List<TicketSnapshot>(liveTickets.Count + archivedTickets.Count);
        foreach (var ticket in liveTickets)
        {
            ticket.CommentCount = commentCounts.TryGetValue(ticket.TicketId, out var count)
                ? count
                : 0;
            all.Add(ticket);
        }

        foreach (var ticket in archivedTickets)
        {
            ticket.CommentCount = ticket.ArchivedCommentCount;
            all.Add(ticket);
        }

        var now = DateTime.UtcNow;
        var groupsByKey = new Dictionary<string, GroupSnapshot>(StringComparer.Ordinal);

        foreach (var ticket in all)
        {
            var tokens = Tokenize(ticket.Title);
            if (tokens.Count == 0)
            {
                continue;
            }

            var signature = tokens.Take(MaxSignatureTokens).ToList();
            if (signature.Count == 0)
            {
                continue;
            }

            var groupKey = BuildGroupKey(ticket.BoardId, signature);
            if (!groupsByKey.TryGetValue(groupKey, out var group))
            {
                group = new GroupSnapshot
                {
                    GroupKey = groupKey,
                    BoardId = ticket.BoardId,
                    BoardName = boardNames.TryGetValue(ticket.BoardId, out var boardName)
                        ? boardName
                        : $"Board {ticket.BoardId}",
                    SignatureTokens = signature,
                };
                groupsByKey[groupKey] = group;
            }

            var owner = NormalizeOwner(ticket.SynitiOwner, ticket.BusinessOwner);
            group.Tickets.Add(new GroupTicket
            {
                TicketId = ticket.TicketId,
                Title = ticket.Title,
                Priority = string.IsNullOrWhiteSpace(ticket.Priority) ? "Medium" : ticket.Priority,
                Status = ticket.Status ?? string.Empty,
                IsArchived = ticket.IsArchived,
                CreatedDate = ticket.CreatedDate,
                ClosedDate = ResolveClosedDate(ticket),
                ResolutionHours = ResolveResolutionHours(ticket),
                CommentCount = ticket.CommentCount,
                Owner = owner,
                BoardName = boardNames.TryGetValue(ticket.BoardId, out var boardLookup)
                    ? boardLookup
                    : $"Board {ticket.BoardId}",
            });
        }

        foreach (var group in groupsByKey.Values)
        {
            HydrateAggregate(group, now);
        }

        return groupsByKey.Values
            .Where(group => group.RepeatCount >= MinimumGroupSize)
            .ToList();
    }

    private static void HydrateAggregate(GroupSnapshot group, DateTime nowUtc)
    {
        group.RepeatCount = group.Tickets.Count;
        group.OpenCount = group.Tickets.Count(ticket => !IsTerminalStatus(ticket.Status) && !ticket.IsArchived);
        group.FirstSeenUtc = group.Tickets.Min(ticket => ticket.CreatedDate);
        group.LastSeenUtc = group.Tickets.Max(ticket => ticket.CreatedDate);

        var resolved = group.Tickets
            .Where(ticket => ticket.ResolutionHours.HasValue)
            .Select(ticket => ticket.ResolutionHours!.Value)
            .ToList();
        group.TotalResolutionHours = resolved.Sum();
        group.AvgResolutionHours = resolved.Count > 0 ? resolved.Average() : (double?)null;
        group.OperationalTouchCount = group.Tickets.Sum(ticket => ticket.CommentCount);

        var windowStart = nowUtc.AddDays(-30);
        var priorWindowStart = nowUtc.AddDays(-60);
        var recent = group.Tickets.Count(ticket => ticket.CreatedDate >= windowStart);
        var prior = group.Tickets.Count(ticket =>
            ticket.CreatedDate >= priorWindowStart && ticket.CreatedDate < windowStart);

        group.TrendDelta = recent - prior;
        group.TrendLabel = group.TrendDelta switch
        {
            > 0 => "rising",
            < 0 => "falling",
            _ => "stable",
        };

        if (group.Tickets.Count > 0)
        {
            group.RepresentativeTitle = group.Tickets
                .OrderByDescending(ticket => ticket.CreatedDate)
                .First()
                .Title;
        }
    }

    private static RepeatIssueGroupSummary ToSummary(GroupSnapshot group) => new()
    {
        GroupKey = group.GroupKey,
        RepresentativeTitle = group.RepresentativeTitle,
        SignatureTokens = group.SignatureTokens,
        BoardId = group.BoardId,
        BoardName = group.BoardName,
        RepeatCount = group.RepeatCount,
        OpenCount = group.OpenCount,
        FirstSeenUtc = group.FirstSeenUtc,
        LastSeenUtc = group.LastSeenUtc,
        AvgResolutionHours = group.AvgResolutionHours is null
            ? null
            : Math.Round(group.AvgResolutionHours.Value, 1),
        TotalResolutionHours = Math.Round(group.TotalResolutionHours, 1),
        OperationalTouchCount = group.OperationalTouchCount,
        TrendDelta = group.TrendDelta,
        TrendLabel = group.TrendLabel,
    };

    private static DateTime? ResolveClosedDate(TicketSnapshot ticket)
    {
        if (ticket.IsArchived && ticket.ArchivedDate.HasValue)
        {
            return ticket.ArchivedDate;
        }

        if (IsTerminalStatus(ticket.Status) && ticket.LastModifiedDate.HasValue)
        {
            return ticket.LastModifiedDate;
        }

        return null;
    }

    private static double? ResolveResolutionHours(TicketSnapshot ticket)
    {
        var closedAt = ResolveClosedDate(ticket);
        if (!closedAt.HasValue)
        {
            return null;
        }

        var hours = (closedAt.Value - ticket.CreatedDate).TotalHours;
        return hours > 0 ? hours : 0;
    }

    internal static bool IsTerminalStatus(string? status)
    {
        return !string.IsNullOrWhiteSpace(status) && TerminalStatuses.Contains(status.Trim());
    }

    private static string? NormalizeOwner(string? synitiOwner, string? businessOwner)
    {
        if (!string.IsNullOrWhiteSpace(synitiOwner))
        {
            return synitiOwner.Trim();
        }

        if (!string.IsNullOrWhiteSpace(businessOwner))
        {
            return businessOwner.Trim();
        }

        return null;
    }

    /// <summary>
    /// Exposed for unit testing. Produces a stable, alphabetically-ordered list of up to
    /// <see cref="MaxSignatureTokens"/> non-stopword tokens of length &gt;= <see cref="MinTokenLength"/>.
    /// </summary>
    internal static List<string> Tokenize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        var builder = new StringBuilder(title.Length);
        foreach (var character in title)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLower(character, CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(' ');
            }
        }

        var tokens = builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= MinTokenLength)
            .Where(token => !Stopwords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToList();

        return tokens;
    }

    private static string BuildGroupKey(int boardId, List<string> signatureTokens)
    {
        return $"b{boardId}-{string.Join('-', signatureTokens)}";
    }

    private sealed class TicketSnapshot
    {
        public string TicketId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int BoardId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public string? SynitiOwner { get; set; }
        public string? BusinessOwner { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedDate { get; set; }
        public int ArchivedCommentCount { get; set; }
        public int CommentCount { get; set; }
    }

    private sealed class GroupSnapshot
    {
        public string GroupKey { get; set; } = string.Empty;
        public string RepresentativeTitle { get; set; } = string.Empty;
        public List<string> SignatureTokens { get; set; } = [];
        public int BoardId { get; set; }
        public string BoardName { get; set; } = string.Empty;

        public int RepeatCount { get; set; }
        public int OpenCount { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public double? AvgResolutionHours { get; set; }
        public double TotalResolutionHours { get; set; }
        public int OperationalTouchCount { get; set; }
        public int TrendDelta { get; set; }
        public string TrendLabel { get; set; } = "stable";

        public List<GroupTicket> Tickets { get; set; } = [];
    }

    private sealed class GroupTicket
    {
        public string TicketId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsArchived { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public double? ResolutionHours { get; set; }
        public int CommentCount { get; set; }
        public string? Owner { get; set; }
        public string BoardName { get; set; } = string.Empty;
    }
}
