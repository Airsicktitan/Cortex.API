using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Cortex.API.Data;

namespace Cortex.API.Hubs;

[Authorize]
public class RealtimeHub(IUserRepository userRepository) : Hub
{
    private readonly IUserRepository _userRepository = userRepository;

    public override async Task OnConnectedAsync()
    {
        var auth0Id = Context.User?.FindFirst("sub")?.Value;
        var email = Context.User?.FindFirst("email")?.Value;

        Cortex.API.Models.User? user = null;
        if (!string.IsNullOrWhiteSpace(auth0Id))
        {
            user = await _userRepository.GetByAuth0IdAsync(auth0Id);
        }

        if (user is null && !string.IsNullOrWhiteSpace(email))
        {
            user = await _userRepository.GetByEmailAsync(email);
        }

        if (user is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeHubGroups.ForUser(user.Id));
        }

        await base.OnConnectedAsync();
    }
}
