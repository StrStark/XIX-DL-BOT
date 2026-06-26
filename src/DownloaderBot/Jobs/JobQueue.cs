using System.Threading.Channels;
using DownloaderBot.Configuration;

namespace DownloaderBot.Jobs;

public sealed class JobQueue
{
    readonly Channel<BotJob> _channel;
    public JobQueue(BotOptions o)
    {
        _channel = Channel.CreateBounded<BotJob>(new BoundedChannelOptions(o.JobQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(BotJob job, CancellationToken ct) => _channel.Writer.WriteAsync(job, ct);
    public ChannelReader<BotJob> Reader => _channel.Reader;
    public void Complete() => _channel.Writer.TryComplete();
}
