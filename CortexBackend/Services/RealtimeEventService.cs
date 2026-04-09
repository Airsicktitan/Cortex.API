using System.Collections.Concurrent;
using System.Threading.Channels;
using Cortex.API.DTO;

namespace Cortex.API.Services;

public class RealtimeEventService : IRealtimeEventService
{
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
}
