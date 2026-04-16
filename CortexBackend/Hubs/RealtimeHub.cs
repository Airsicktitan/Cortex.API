using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cortex.API.Hubs;

[Authorize]
public class RealtimeHub : Hub
{
}
