using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Cortex.API.Services;

namespace Cortex.API.Hubs;

/// <summary>
/// Realtime hub for ticket/comment/notification streams.
/// </summary>
/// <remarks>
/// Identity resolution MUST go through <see cref="IUserContextService.GetCurrentUserAsync"/>
/// so the hub uses the same approval-enforcing gate as the HTTP API. A connection that
/// cannot be resolved to an approved Cortex user is aborted — we do not establish a
/// realtime channel and we do not fall back to matching by email (email fallback used to
/// allow an unknown Auth0 subject to inherit another local user's group membership).
/// </remarks>
[Authorize]
public class RealtimeHub(
    IUserContextService userContext,
    ILogger<RealtimeHub> logger) : Hub
{
    private readonly IUserContextService _userContext = userContext;
    private readonly ILogger<RealtimeHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        try
        {
            var user = await _userContext.GetCurrentUserAsync(
                Context.User,
                Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RealtimeHubGroups.ForUser(user.Id));
            await base.OnConnectedAsync();
        }
        catch (AccessNotApprovedException ex)
        {
            _logger.LogWarning(
                "Realtime hub connection rejected for unapproved identity. Reason={Reason}, Email={Email}, Auth0Id={Auth0Id}, ConnectionId={ConnectionId}",
                ex.Reason,
                ex.Email ?? "(unknown)",
                ex.Auth0Id ?? "(unknown)",
                Context.ConnectionId);
            Context.Abort();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Realtime hub connection rejected: missing/invalid authenticated identity. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            Context.Abort();
        }
    }
}
