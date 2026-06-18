using HackerNews.Application.Abstractions;
using HackerNews.Application.Snapshots;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HackerNews.Infrastructure.HostedServices;

internal sealed class SnapshotSubscriberService : BackgroundService
{
    private readonly ISnapshotStore _store;
    private readonly ISnapshotChannel _channel;
    private readonly IBestStoriesCacheWriter _cacheWriter;
    private readonly ILogger<SnapshotSubscriberService> _logger;

    public SnapshotSubscriberService(
        ISnapshotStore store,
        ISnapshotChannel channel,
        IBestStoriesCacheWriter cacheWriter,
        ILogger<SnapshotSubscriberService> logger)
    {
        _store = store;
        _channel = channel;
        _cacheWriter = cacheWriter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PrimeAsync(stoppingToken);

        await _channel.SubscribeAsync(OnSnapshotAsync, PrimeAsync, stoppingToken);
        _logger.LogInformation("Subscribed to snapshot updates");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Task OnSnapshotAsync(SnapshotState state, CancellationToken cancellationToken)
    {
        if (_cacheWriter.TryUpdate(state.Version, state.Stories))
            _logger.LogInformation("Applied snapshot version {Version} ({Count} stories) via pub/sub",
                state.Version, state.Stories.Count);

        return Task.CompletedTask;
    }

    private async Task PrimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var state = await _store.LoadAsync(cancellationToken);
            if (state is null)
                return;

            if (_cacheWriter.TryUpdate(state.Version, state.Stories))
                _logger.LogInformation("Primed snapshot version {Version} ({Count} stories) from Redis",
                    state.Version, state.Stories.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prime snapshot from Redis");
        }
    }
}
