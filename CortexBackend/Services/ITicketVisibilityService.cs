using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ITicketVisibilityService
{
    Task<TicketVisibilityContext> GetCurrentVisibilityAsync();
}

public enum TicketVisibilityScope
{
    All,
    /// <summary>
    /// Reviewer-facing intake visibility: stranger tickets remain limited to PendingApproval /
    /// NeedsMoreInfo queues; creators always see own tickets regardless of lifecycle.
    /// </summary>
    PendingApprover,
    AssignedToCurrentUser,
    CreatedByCurrentUser,
}

public sealed record TicketVisibilityContext(
    int UserId,
    string? DisplayName,
    string? Email,
    TicketVisibilityScope Scope)
{
    public bool CanView(Ticket ticket)
    {
        if (Scope == TicketVisibilityScope.PendingApprover &&
            (ticket.ApprovalStatus == ApprovalStatus.PendingApproval ||
             ticket.ApprovalStatus == ApprovalStatus.NeedsMoreInfo ||
             ticket.CreatedBy == UserId))
        {
            return true;
        }

        return CanView(ticket.CreatedBy, ticket.SynitiOwner, ticket.BusinessOwner);
    }

    public bool CanView(int createdBy, string? synitiOwner, string? businessOwner)
    {
        return Scope switch
        {
            TicketVisibilityScope.All => true,
            TicketVisibilityScope.PendingApprover => createdBy == UserId,
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

        var trimmed = value.Trim();
        if (trimmed.StartsWith(OwnerFieldResolution.UserIdTokenPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(trimmed.AsSpan(OwnerFieldResolution.UserIdTokenPrefix.Length), out var ownerId) &&
            ownerId == UserId)
        {
            return true;
        }

        return string.Equals(trimmed, DisplayName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, Email?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
