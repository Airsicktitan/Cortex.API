using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ITicketVisibilityService
{
    Task<TicketVisibilityContext> GetCurrentVisibilityAsync();
}

public enum TicketVisibilityScope
{
    All,
    AssignedToCurrentUser,
    CreatedByCurrentUser
}

public sealed record TicketVisibilityContext(
    int UserId,
    string? DisplayName,
    string? Email,
    TicketVisibilityScope Scope)
{
    public bool CanView(Ticket ticket)
    {
        return Scope switch
        {
            TicketVisibilityScope.All => true,
            TicketVisibilityScope.AssignedToCurrentUser => IsAssignedToCurrentUser(ticket),
            _ => ticket.CreatedBy == UserId
        };
    }

    private bool IsAssignedToCurrentUser(Ticket ticket)
    {
        return MatchesIdentity(ticket.SynitiOwner) || MatchesIdentity(ticket.BusinessOwner);
    }

    private bool MatchesIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value.Trim(), DisplayName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value.Trim(), Email?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
