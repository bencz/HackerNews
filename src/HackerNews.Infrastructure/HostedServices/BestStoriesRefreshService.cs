using HackerNews.Application.Abstractions;
using HackerNews.Application.Configuration;
using HackerNews.Application.Snapshots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace HackerNews.Infrastructure.HostedServices;

internal sealed class BestStoriesRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLock _distributedLock;
    private readonly ISnapshotStore _store;
    private readonly ISnapshotChannel _channel;
    private readonly IBestStoriesCacheWriter _cacheWriter;
    private readonly HackerNewsOptions _options;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<BestStoriesRefreshService> _logger;

    public BestStoriesRefreshService(
        IServiceScopeFactory scopeFactory,
        IDistributedLock distributedLock,
        ISnapshotStore store,
        ISnapshotChannel channel,
        IBestStoriesCacheWriter cacheWriter,
        IOptions<HackerNewsOptions> options,
        IOptions<RedisOptions> redisOptions,
        ILogger<BestStoriesRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _store = store;
        _channel = channel;
        _cacheWriter = cacheWriter;
        _options = options.Value;
        _redisOptions = redisOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.RefreshIntervalSeconds);
        var lockWait = TimeSpan.FromSeconds(_redisOptions.LockWaitSeconds);
        _logger.LogInformation("Refresh service started. Interval: {Interval}", interval);

        using var timer = new PeriodicTimer(interval);

        do
        {
            using (LogContext.PushProperty("CorrelationId", Guid.NewGuid()))
            {
                try
                {
                    await TryRefreshAsync(interval, lockWait, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Refresh cycle failed");
                }
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task TryRefreshAsync(TimeSpan interval, TimeSpan lockWait, CancellationToken cancellationToken)
    {
        await using var lease = await _distributedLock.TryAcquireAsync(lockWait, cancellationToken);
        if (lease is null)
        {
            _logger.LogDebug("Refresh lock held by another node; skipping this cycle");
            return;
        }

        var meta = await _store.GetMetaAsync(cancellationToken);
        if (meta is not null && DateTimeOffset.UtcNow - meta.UpdatedAt < interval)
        {
            _logger.LogDebug("Snapshot still fresh; skipping refresh");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBestStoriesService>();

        var stories = await service.RefreshAsync(cancellationToken);
        if (stories is null || stories.Count == 0)
            return;

        var updatedAt = DateTimeOffset.UtcNow;
        var version = await _store.SaveAsync(stories, updatedAt, cancellationToken);
        _cacheWriter.TryUpdate(version, stories);
        await _channel.PublishAsync(new SnapshotState(version, stories, updatedAt), cancellationToken);

        _logger.LogInformation("Published snapshot version {Version}", version);
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
