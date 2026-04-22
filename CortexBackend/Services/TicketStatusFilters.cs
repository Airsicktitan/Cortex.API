namespace Cortex.API.Services;

public static class TicketStatusFilters
{
    /// <summary>
    /// Upper-cased status names treated as resolved for SQL-translatable query predicates.
    /// Keep aligned with <see cref="TicketSlaCalculator.IsResolvedStatus(string?)"/>.
    /// </summary>
    public static readonly string[] ResolvedStatusesUpper = ["RESOLVED", "CLOSED"];
}
