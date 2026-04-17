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
    private readonly ConcurrentDictionary<Guid, RealtimeEventSubscriber> _subscribers = new();

    public RealtimeEventSubscription Subscribe(int userId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<RealtimeEventMessage>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _subscribers[id] = new RealtimeEventSubscriber(userId, channel);

        return new RealtimeEventSubscription(channel.Reader, () =>
        {
            if (_subscribers.TryRemove(id, out var existingSubscriber))
            {
                existingSubscriber.Channel.Writer.TryComplete();
            }
        });
    }

    public ValueTask PublishAsync(
        RealtimeEventMessage message,
        CancellationToken cancellationToken = default)
    {
        var audienceUserIds = ResolveAudienceUserIds(message);
        if (audienceUserIds.Count == 0)
        {
            _logger.LogWarning(
                "Dropping realtime event with no explicit audience. EventType={EventType} TicketId={TicketId} EntityId={EntityId}",
                message.EventType,
                message.TicketId,
                message.EntityId);
            return ValueTask.CompletedTask;
        }

        _ = BroadcastToHubBestEffortAsync(message, audienceUserIds, cancellationToken);

        foreach (var subscriber in _subscribers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!audienceUserIds.Contains(subscriber.Value.UserId))
            {
                continue;
            }

            if (!subscriber.Value.Channel.Writer.TryWrite(message) &&
                _subscribers.TryRemove(subscriber.Key, out var staleChannel))
            {
                staleChannel.Channel.Writer.TryComplete();
            }
        }

        return ValueTask.CompletedTask;
    }

    private async Task BroadcastToHubBestEffortAsync(
        RealtimeEventMessage message,
        IReadOnlyCollection<int> audienceUserIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var audienceGroups = audienceUserIds
                .Select(RealtimeHubGroups.ForUser)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            await _hubContext.Clients
                .Groups(audienceGroups)
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

    private static HashSet<int> ResolveAudienceUserIds(RealtimeEventMessage message)
    {
        var rawAudience = message.AudienceUserIds ?? message.RecipientUserIds ?? [];
        return rawAudience
            .Where(userId => userId > 0)
            .ToHashSet();
    }

    private sealed record RealtimeEventSubscriber(
        int UserId,
        Channel<RealtimeEventMessage> Channel);
}
