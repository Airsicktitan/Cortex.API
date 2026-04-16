using System.Collections.Concurrent;
using System.Threading.Channels;
using Cortex.API.DTO;
using Cortex.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Cortex.API.Services;

public class RealtimeEventService(
    IHubContext<RealtimeHub> hubContext,
    ILogger<RealtimeEventService> logger) : IRealtimeEventService
{
    private readonly IHubContext<RealtimeHub> _hubContext = hubContext;
    private readonly ILogger<RealtimeEventService> _logger = logger;
    private readonly ConcurrentDictionary<Guid, Channel<RealtimeEventMessage>> _subscribers = new();

    public RealtimeEventSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<RealtimeEventMessage>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _subscribers[id] = channel;

        return new RealtimeEventSubscription(channel.Reader, () =>
        {
            if (_subscribers.TryRemove(id, out var existingChannel))
            {
                existingChannel.Writer.TryComplete();
            }
        });
    }

    public ValueTask PublishAsync(
        RealtimeEventMessage message,
        CancellationToken cancellationToken = default)
    {
        _ = BroadcastToHubBestEffortAsync(message, cancellationToken);

        foreach (var subscriber in _subscribers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!subscriber.Value.Writer.TryWrite(message) &&
                _subscribers.TryRemove(subscriber.Key, out var staleChannel))
            {
                staleChannel.Writer.TryComplete();
            }
        }

        return ValueTask.CompletedTask;
    }

    private async Task BroadcastToHubBestEffortAsync(
        RealtimeEventMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients
                .All
                .SendAsync("realtime", message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Best-effort publish; request cancellation should not fail mutation paths.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Realtime hub broadcast failed. EventType={EventType} TicketId={TicketId} EntityId={EntityId}",
                message.EventType,
                message.TicketId,
                message.EntityId);
        }
    }
}
