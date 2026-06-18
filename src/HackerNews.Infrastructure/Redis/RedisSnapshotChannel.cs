using System.Text.Json;
using HackerNews.Application.Abstractions;
using HackerNews.Application.Configuration;
using HackerNews.Application.Snapshots;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HackerNews.Infrastructure.Redis;

internal sealed class RedisSnapshotChannel : ISnapshotChannel
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RedisChannel _channel;

    public RedisSnapshotChannel(IConnectionMultiplexer multiplexer, IOptions<RedisOptions> options)
    {
        _multiplexer = multiplexer;
        _channel = RedisChannel.Literal(options.Value.UpdatesChannel);
    }

    public async Task PublishAsync(SnapshotState state, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(state, SnapshotJsonContext.Default.SnapshotState);
        var subscriber = _multiplexer.GetSubscriber();
        await subscriber.PublishAsync(_channel, payload);
    }

    public async Task SubscribeAsync(
        Func<SnapshotState, CancellationToken, Task> onSnapshot,
        Func<CancellationToken, Task> onReconnected,
        CancellationToken cancellationToken)
    {
        var subscriber = _multiplexer.GetSubscriber();
        var queue = await subscriber.SubscribeAsync(_channel);

        queue.OnMessage(async message =>
        {
            if (message.Message.IsNullOrEmpty)
                return;

            var state = JsonSerializer.Deserialize((byte[])message.Message, SnapshotJsonContext.Default.SnapshotState);
            if (state is not null)
                await onSnapshot(state, cancellationToken);
        });

        _multiplexer.ConnectionRestored += async (_, _) =>
        {
            try
            {
                await onReconnected(cancellationToken);
            }
            catch
            {
            }
        };
    }
}
