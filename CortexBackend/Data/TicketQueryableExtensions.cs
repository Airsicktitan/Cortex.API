using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Data;

internal static class TicketQueryableExtensions
{
    public static IQueryable<Ticket> WhereVisibleTo(
        this IQueryable<Ticket> query,
        TicketVisibilityContext ctx)
    {
        return ctx.Scope switch
        {
            TicketVisibilityScope.All => query,
            TicketVisibilityScope.CreatedByCurrentUser => query.Where(t => t.CreatedBy == ctx.UserId),
            TicketVisibilityScope.AssignedToCurrentUser => WhereAssignedToCurrentUser(query, ctx),
            _ => query.Where(t => t.CreatedBy == ctx.UserId)
        };
    }

    private static IQueryable<Ticket> WhereAssignedToCurrentUser(
        IQueryable<Ticket> query,
        TicketVisibilityContext ctx)
    {
        var userIdToken = OwnerFieldResolution.UserIdTokenPrefix + ctx.UserId;
        return query.Where(t =>
            (ctx.DisplayName != null && t.SynitiOwner != null &&
             string.Equals(
                 t.SynitiOwner.Trim(),
                 ctx.DisplayName.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (ctx.Email != null && t.SynitiOwner != null &&
             string.Equals(
                 t.SynitiOwner.Trim(),
                 ctx.Email.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (ctx.DisplayName != null && t.BusinessOwner != null &&
             string.Equals(
                 t.BusinessOwner.Trim(),
                 ctx.DisplayName.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (ctx.Email != null && t.BusinessOwner != null &&
             string.Equals(
                 t.BusinessOwner.Trim(),
                 ctx.Email.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (t.SynitiOwner != null &&
             string.Equals(t.SynitiOwner.Trim(), userIdToken, StringComparison.OrdinalIgnoreCase)) ||
            (t.BusinessOwner != null &&
             string.Equals(t.BusinessOwner.Trim(), userIdToken, StringComparison.OrdinalIgnoreCase)));
    }

    public static IQueryable<ArchivedTicket> WhereVisibleTo(
        this IQueryable<ArchivedTicket> query,
        TicketVisibilityContext ctx)
    {
        return ctx.Scope switch
        {
            TicketVisibilityScope.All => query,
            TicketVisibilityScope.CreatedByCurrentUser => query.Where(t => t.CreatedBy == ctx.UserId),
            TicketVisibilityScope.AssignedToCurrentUser => WhereArchivedAssignedToCurrentUser(query, ctx),
            _ => query.Where(t => t.CreatedBy == ctx.UserId)
        };
    }

    private static IQueryable<ArchivedTicket> WhereArchivedAssignedToCurrentUser(
        IQueryable<ArchivedTicket> query,
        TicketVisibilityContext ctx)
    {
        var userIdToken = OwnerFieldResolution.UserIdTokenPrefix + ctx.UserId;
        return query.Where(t =>
            (ctx.DisplayName != null && t.SynitiOwner != null &&
             string.Equals(
                 t.SynitiOwner.Trim(),
                 ctx.DisplayName.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (ctx.Email != null && t.SynitiOwner != null &&
             string.Equals(
                 t.SynitiOwner.Trim(),
                 ctx.Email.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (ctx.DisplayName != null && t.BusinessOwner != null &&
             string.Equals(
                 t.BusinessOwner.Trim(),
                 ctx.DisplayName.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (ctx.Email != null && t.BusinessOwner != null &&
             string.Equals(
                 t.BusinessOwner.Trim(),
                 ctx.Email.Trim(),
                 StringComparison.OrdinalIgnoreCase)) ||
            (t.SynitiOwner != null &&
             string.Equals(t.SynitiOwner.Trim(), userIdToken, StringComparison.OrdinalIgnoreCase)) ||
            (t.BusinessOwner != null &&
             string.Equals(t.BusinessOwner.Trim(), userIdToken, StringComparison.OrdinalIgnoreCase)));
    }

    public static IQueryable<Ticket> OrderByTicketListSort(this IQueryable<Ticket> query, string sort)
    {
        return sort switch
        {
            "oldest-first" => query
                .OrderBy(t => t.CreatedDate)
                .ThenBy(t => t.Id),
            "priority-high-low" => query
                .OrderByDescending(t =>
                    t.Priority == "Critical" ? 4 :
                    t.Priority == "High" ? 3 :
                    t.Priority == "Medium" ? 2 :
                    t.Priority == "Low" ? 1 : 0)
                .ThenByDescending(t => t.CreatedDate)
                .ThenByDescending(t => t.Id),
            "priority-low-high" => query
                .OrderBy(t =>
                    t.Priority == "Critical" ? 4 :
                    t.Priority == "High" ? 3 :
                    t.Priority == "Medium" ? 2 :
                    t.Priority == "Low" ? 1 : 0)
                .ThenByDescending(t => t.CreatedDate)
                .ThenByDescending(t => t.Id),
            _ => query
                .OrderByDescending(t => t.CreatedDate)
                .ThenByDescending(t => t.Id)
        };
    }

    /// <summary>In-memory ordering mirroring <see cref="OrderByTicketListSort"/> for unpaged exports.</summary>
    public static List<Ticket> SortTicketEntitiesInMemory(IReadOnlyList<Ticket> tickets, string sort)
    {
        return sort switch
        {
            "oldest-first" => tickets.OrderBy(t => t.CreatedDate).ThenBy(t => t.Id).ToList(),
            "priority-high-low" => tickets
                .OrderByDescending(t =>
                    t.Priority == "Critical" ? 4 :
                    t.Priority == "High" ? 3 :
                    t.Priority == "Medium" ? 2 :
                    t.Priority == "Low" ? 1 : 0)
                .ThenByDescending(t => t.CreatedDate)
                .ThenByDescending(t => t.Id)
                .ToList(),
            "priority-low-high" => tickets
                .OrderBy(t =>
                    t.Priority == "Critical" ? 4 :
                    t.Priority == "High" ? 3 :
                    t.Priority == "Medium" ? 2 :
                    t.Priority == "Low" ? 1 : 0)
                .ThenByDescending(t => t.CreatedDate)
                .ThenByDescending(t => t.Id)
                .ToList(),
            _ => tickets
                .OrderByDescending(t => t.CreatedDate)
                .ThenByDescending(t => t.Id)
                .ToList()
        };
    }
}
