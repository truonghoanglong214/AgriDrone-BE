using System.Threading.Channels;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class MissionProcessingQueue : IMissionProcessingQueue
{
    private readonly Channel<MissionProcessingWorkItem> _channel =
        Channel.CreateUnbounded<MissionProcessingWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(
        MissionProcessingWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return _channel.Writer.WriteAsync(workItem, cancellationToken);
    }

    public IAsyncEnumerable<MissionProcessingWorkItem> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
