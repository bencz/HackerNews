using HackerNews.Application.Abstractions;
using HackerNews.Application.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HackerNews.Infrastructure.Redis;

internal sealed class RedisDistributedLock : IDistributedLock
{
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        else
            return 0
        end
        """;

    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(200);

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RedisOptions _options;

    public RedisDistributedLock(IConnectionMultiplexer multiplexer, IOptions<RedisOptions> options)
    {
        _multiplexer = multiplexer;
        _options = options.Value;
    }

    public async Task<IAsyncDisposable> TryAcquireAsync(TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var db = _multiplexer.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var ttl = TimeSpan.FromSeconds(_options.LockTtlSeconds);
        var deadline = DateTimeOffset.UtcNow + maxWait;

        while (true)
        {
            if (await db.StringSetAsync(_options.LockKey, token, ttl, when: When.NotExists))
                return new Lease(db, _options.LockKey, token);

            if (DateTimeOffset.UtcNow >= deadline)
                return null;

            await Task.Delay(PollDelay, cancellationToken);
        }
    }

    private sealed class Lease(IDatabase db, RedisKey key, RedisValue token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await db.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
        }
    }
}
