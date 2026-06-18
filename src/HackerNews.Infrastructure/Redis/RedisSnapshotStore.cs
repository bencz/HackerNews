using System.Text.Json;
using HackerNews.Application.Abstractions;
using HackerNews.Application.Configuration;
using HackerNews.Application.Snapshots;
using HackerNews.Domain;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HackerNews.Infrastructure.Redis;

internal sealed class RedisSnapshotStore : ISnapshotStore
{
    private const string SaveScript = """
        local v = redis.call('HINCRBY', KEYS[1], 'version', 1)
        redis.call('HSET', KEYS[1], 'payload', ARGV[1], 'updatedAt', ARGV[2])
        return v
        """;

    private static readonly RedisValue VersionField = "version";
    private static readonly RedisValue PayloadField = "payload";
    private static readonly RedisValue UpdatedAtField = "updatedAt";

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RedisOptions _options;

    public RedisSnapshotStore(IConnectionMultiplexer multiplexer, IOptions<RedisOptions> options)
    {
        _multiplexer = multiplexer;
        _options = options.Value;
    }

    public async Task<SnapshotMeta> GetMetaAsync(CancellationToken cancellationToken)
    {
        var db = _multiplexer.GetDatabase();
        var values = await db.HashGetAsync(_options.StateKey, [VersionField, UpdatedAtField]);

        if (values[0].IsNullOrEmpty)
            return null;

        return new SnapshotMeta((long)values[0], ParseTimestamp(values[1]));
    }

    public async Task<SnapshotState> LoadAsync(CancellationToken cancellationToken)
    {
        var db = _multiplexer.GetDatabase();
        var values = await db.HashGetAsync(_options.StateKey, [VersionField, PayloadField, UpdatedAtField]);

        if (values[1].IsNullOrEmpty)
            return null;

        var stories = JsonSerializer.Deserialize((byte[])values[1], SnapshotJsonContext.Default.StoryArray) ?? [];
        return new SnapshotState((long)values[0], stories, ParseTimestamp(values[2]));
    }

    public async Task<long> SaveAsync(
        IReadOnlyList<Story> stories, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            stories as Story[] ?? [.. stories], SnapshotJsonContext.Default.StoryArray);

        var db = _multiplexer.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            SaveScript,
            [_options.StateKey],
            [payload, updatedAt.ToUnixTimeMilliseconds()]);

        return (long)result;
    }

    private static DateTimeOffset ParseTimestamp(RedisValue value) =>
        value.IsNullOrEmpty
            ? DateTimeOffset.MinValue
            : DateTimeOffset.FromUnixTimeMilliseconds((long)value);
}
