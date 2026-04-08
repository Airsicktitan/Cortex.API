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
        return CanView(ticket.CreatedBy, ticket.SynitiOwner, ticket.BusinessOwner);
    }

    public bool CanView(int createdBy, string? synitiOwner, string? businessOwner)
    {
        return Scope switch
        {
            TicketVisibilityScope.All => true,
            TicketVisibilityScope.AssignedToCurrentUser => IsAssignedToCurrentUser(synitiOwner, businessOwner),
            _ => createdBy == UserId
        };
    }

    private bool IsAssignedToCurrentUser(string? synitiOwner, string? businessOwner)
    {
        return MatchesIdentity(synitiOwner) || MatchesIdentity(businessOwner);
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
