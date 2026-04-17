using System.Text.Json;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class RealtimeHandlers
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(20);

    public static async Task StreamEvents(
        HttpContext context,
        IRealtimeEventService realtimeEventService,
        IUserContextService userContextService)
    {
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Response.ContentType = "text/event-stream";

        await context.Response.WriteAsync(": connected\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);

        var currentUser = await userContextService.GetCurrentUserAsync();
        using var subscription = realtimeEventService.Subscribe(currentUser.Id);

        try
        {
            while (!context.RequestAborted.IsCancellationRequested)
            {
                var waitForEventTask = subscription.Reader.WaitToReadAsync(context.RequestAborted).AsTask();
                var keepAliveTask = Task.Delay(KeepAliveInterval, context.RequestAborted);
                var completedTask = await Task.WhenAny(waitForEventTask, keepAliveTask);

                if (completedTask == keepAliveTask)
                {
                    await context.Response.WriteAsync(": keepalive\n\n", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                    continue;
                }

                if (!await waitForEventTask)
                {
                    break;
                }

                while (subscription.Reader.TryRead(out var message))
                {
                    var json = JsonSerializer.Serialize(message);
                    await context.Response.WriteAsync(
                        $"event: realtime\ndata: {json}\n\n",
                        context.RequestAborted);
                }

                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // The client disconnected.
        }
    }
}
