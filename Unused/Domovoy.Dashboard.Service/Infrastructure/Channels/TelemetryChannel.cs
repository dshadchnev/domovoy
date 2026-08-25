using System.Threading.Channels;
using Domovoy.Shared.Events;

namespace Domovoy.Dashboard.Service.Infrastructure.Channels;

public class TelemetryChannel
{
    private readonly Channel<TelemetryReceivedEvent> _channel;

    public TelemetryChannel(int capacity = 5000)
    {
        // Переведено: Ограниченный буферизованный канал с режимом ожидания при переполнении (Producer-Consumer)
        var options = new BoundedChannelOptions(capacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<TelemetryReceivedEvent>(options);
    }

    public ChannelWriter<TelemetryReceivedEvent> Writer => _channel.Writer;
    public ChannelReader<TelemetryReceivedEvent> Reader => _channel.Reader;
}
