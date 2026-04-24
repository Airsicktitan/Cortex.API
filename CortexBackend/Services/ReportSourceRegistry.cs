namespace Cortex.API.Services;

public sealed record ReportSourceColumn(string Key, string Label, string SqlExpression);

public sealed record ReportSource(
    string Key,
    string Label,
    string Description,
    string FromClause,
    IReadOnlyList<ReportSourceColumn> Columns);

public static class ReportSourceRegistry
{
    /// <summary>
    /// Resolves ticket owner tokens (user:id, email, legacy display/nick) to a readable label via Users.
    /// Fallback chain: DisplayName, NickName, Email, raw ticket value.
    /// </summary>
    private const string SynitiOwnerDisplaySql =
        "COALESCE(NULLIF(LTRIM(RTRIM(cortex_so.DisplayName)), N''), NULLIF(LTRIM(RTRIM(cortex_so.NickName)), N''), NULLIF(LTRIM(RTRIM(cortex_so.Email)), N''), NULLIF(LTRIM(RTRIM(t.SynitiOwner)), N''))";

    private const string BusinessOwnerDisplaySql =
        "COALESCE(NULLIF(LTRIM(RTRIM(cortex_bo.DisplayName)), N''), NULLIF(LTRIM(RTRIM(cortex_bo.NickName)), N''), NULLIF(LTRIM(RTRIM(cortex_bo.Email)), N''), NULLIF(LTRIM(RTRIM(t.BusinessOwner)), N''))";

    private const string SynitiOwnerOuterApply = """
        OUTER APPLY (
          SELECT TOP 1 u.DisplayName, u.NickName, u.Email
          FROM Users u
          WHERE LTRIM(RTRIM(ISNULL(t.SynitiOwner, N''))) <> N''
            AND (
              LOWER(LTRIM(RTRIM(t.SynitiOwner))) = LOWER(CONCAT(N'user:', CAST(u.Id AS nvarchar(20))))
              OR LOWER(LTRIM(RTRIM(t.SynitiOwner))) = LOWER(LTRIM(RTRIM(u.Email)))
              OR (u.DisplayName IS NOT NULL AND LOWER(LTRIM(RTRIM(t.SynitiOwner))) = LOWER(LTRIM(RTRIM(u.DisplayName))))
              OR (u.NickName IS NOT NULL AND LOWER(LTRIM(RTRIM(t.SynitiOwner))) = LOWER(LTRIM(RTRIM(u.NickName))))
            )
          ORDER BY
            CASE WHEN LOWER(LTRIM(RTRIM(t.SynitiOwner))) = LOWER(CONCAT(N'user:', CAST(u.Id AS nvarchar(20)))) THEN 0 ELSE 1 END,
            CASE WHEN LOWER(LTRIM(RTRIM(t.SynitiOwner))) = LOWER(LTRIM(RTRIM(u.Email))) THEN 0 ELSE 1 END,
            u.Id
        ) cortex_so
        """;

    private const string BusinessOwnerOuterApply = """
        OUTER APPLY (
          SELECT TOP 1 u.DisplayName, u.NickName, u.Email
          FROM Users u
          WHERE LTRIM(RTRIM(ISNULL(t.BusinessOwner, N''))) <> N''
            AND (
              LOWER(LTRIM(RTRIM(t.BusinessOwner))) = LOWER(CONCAT(N'user:', CAST(u.Id AS nvarchar(20))))
              OR LOWER(LTRIM(RTRIM(t.BusinessOwner))) = LOWER(LTRIM(RTRIM(u.Email)))
              OR (u.DisplayName IS NOT NULL AND LOWER(LTRIM(RTRIM(t.BusinessOwner))) = LOWER(LTRIM(RTRIM(u.DisplayName))))
              OR (u.NickName IS NOT NULL AND LOWER(LTRIM(RTRIM(t.BusinessOwner))) = LOWER(LTRIM(RTRIM(u.NickName))))
            )
          ORDER BY
            CASE WHEN LOWER(LTRIM(RTRIM(t.BusinessOwner))) = LOWER(CONCAT(N'user:', CAST(u.Id AS nvarchar(20)))) THEN 0 ELSE 1 END,
            CASE WHEN LOWER(LTRIM(RTRIM(t.BusinessOwner))) = LOWER(LTRIM(RTRIM(u.Email))) THEN 0 ELSE 1 END,
            u.Id
        ) cortex_bo
        """;

    private static readonly IReadOnlyList<ReportSourceColumn> TicketColumns =
    [
        new("id",                 "Ticket ID",          "t.Id"),
        new("title",              "Title",              "t.Title"),
        new("status",             "Status",             "t.Status"),
        new("priority",           "Priority",           "t.Priority"),
        new("board",              "Board",              "b.Name"),
        new("story_points",       "Story Points",       "t.StoryPoints"),
        new("syniti_owner",       "Syniti Owner",       SynitiOwnerDisplaySql),
        new("business_owner",     "Business Owner",     BusinessOwnerDisplaySql),
        new("created_date",       "Created Date",       "t.CreatedDate"),
        new("last_modified_date", "Last Modified Date", "t.LastModifiedDate"),
    ];

    public static readonly IReadOnlyList<ReportSource> Sources =
    [
        new(
            "tickets",
            "All Tickets",
            "Every ticket in the system, including resolved and closed.",
            BuildTicketFromClause("Tickets", null),
            TicketColumns),
        new(
            "open_tickets",
            "Open Tickets",
            "Active tickets that have not yet been resolved or closed.",
            BuildTicketFromClause(
                "Tickets",
                "WHERE t.Status NOT IN (N'Resolved', N'Closed', N'Cancelled')"),
            TicketColumns),
        new(
            "archived_tickets",
            "Archived Tickets",
            "Tickets that have been archived and removed from the active workspace.",
            BuildTicketFromClause("ArchivedTickets", null),
            TicketColumns),
    ];

    private static readonly IReadOnlyDictionary<string, ReportSource> SourceByKey =
        Sources.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

    public static ReportSource? TryGet(string key) =>
        SourceByKey.TryGetValue(key, out var source) ? source : null;

    public static string GenerateSql(string sourceKey, string? selectedColumnsCsv)
    {
        var source = TryGet(sourceKey)
            ?? throw new ArgumentException($"Unknown report source '{sourceKey}'.");

        var selectedKeys = string.IsNullOrWhiteSpace(selectedColumnsCsv)
            ? []
            : selectedColumnsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var columns = selectedKeys.Length > 0
            ? source.Columns
                .Where(c => selectedKeys.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
                .ToList()
            : (IReadOnlyList<ReportSourceColumn>)source.Columns;

        if (columns.Count == 0)
        {
            columns = source.Columns;
        }

        var needsSynitiOwnerJoin = columns.Any(c =>
            string.Equals(c.Key, "syniti_owner", StringComparison.OrdinalIgnoreCase));
        var needsBusinessOwnerJoin = columns.Any(c =>
            string.Equals(c.Key, "business_owner", StringComparison.OrdinalIgnoreCase));

        var fromClause = PatchFromClauseForOwnerJoins(
            source.FromClause,
            needsSynitiOwnerJoin,
            needsBusinessOwnerJoin);

        var selectList = string.Join(",\r\n  ", columns.Select(c => $"{c.SqlExpression} AS [{c.Label}]"));
        return $"SELECT\r\n  {selectList}\r\n{fromClause}";
    }

    /// <summary>
    /// Base sources store a placeholder so <see cref="GenerateSql"/> can inject OUTER APPLY blocks only when needed.
    /// </summary>
    private static string BuildTicketFromClause(string primaryTable, string? trailingWhereClause)
    {
        var tail = string.IsNullOrWhiteSpace(trailingWhereClause)
            ? string.Empty
            : "\r\n" + trailingWhereClause.Trim();
        return $"FROM {primaryTable} t\r\nLEFT JOIN TicketBoardDefinitions b ON t.BoardId = b.Id\r\n{{CORTEX_OWNER_JOINS}}{tail}";
    }

    private static string PatchFromClauseForOwnerJoins(
        string fromClause,
        bool needsSynitiOwnerJoin,
        bool needsBusinessOwnerJoin)
    {
        var joins = string.Empty;
        if (needsSynitiOwnerJoin)
        {
            joins += SynitiOwnerOuterApply.Trim() + "\r\n";
        }

        if (needsBusinessOwnerJoin)
        {
            joins += BusinessOwnerOuterApply.Trim() + "\r\n";
        }

        return fromClause.Replace("{CORTEX_OWNER_JOINS}", joins.TrimEnd(), StringComparison.Ordinal);
    }
}
