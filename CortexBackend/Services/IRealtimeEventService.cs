using System.Threading.Channels;
using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface IRealtimeEventService
{
    RealtimeEventSubscription Subscribe(int userId);
    ValueTask PublishAsync(RealtimeEventMessage message, CancellationToken cancellationToken = default);
}

public sealed class RealtimeEventSubscription(
    ChannelReader<RealtimeEventMessage> reader,
    Action dispose) : IDisposable
{
    public ChannelReader<RealtimeEventMessage> Reader { get; } = reader;

    public void Dispose()
    {
        dispose();
    }
}
