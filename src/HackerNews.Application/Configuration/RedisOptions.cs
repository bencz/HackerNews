using System.ComponentModel.DataAnnotations;

namespace HackerNews.Application.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; } = false;

    public string ConnectionString { get; set; } = "";

    public string KeyPrefix { get; set; } = "beststories";

    [Range(5, 300)]
    public int LockTtlSeconds { get; set; } = 30;

    [Range(1, 30)]
    public int LockWaitSeconds { get; set; } = 5;

    public string StateKey => $"{KeyPrefix}:state";

    public string LockKey => $"{KeyPrefix}:lock";

    public string UpdatesChannel => $"{KeyPrefix}:updates";
}
