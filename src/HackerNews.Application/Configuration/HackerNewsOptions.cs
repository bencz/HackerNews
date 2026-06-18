using System.ComponentModel.DataAnnotations;

namespace HackerNews.Application.Configuration;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/";

    [Range(1, 60)]
    public int HttpTimeoutSeconds { get; set; } = 10;

    [Range(5, 3600)]
    public int RefreshIntervalSeconds { get; set; } = 60;

    [Range(1, 500)]
    public int SnapshotSize { get; set; } = 200;

    [Range(1, 100)]
    public int MaxParallelFetches { get; set; } = 20;

    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    [Range(10, 60000)]
    public int RetryMedianFirstDelayMs { get; set; } = 200;

    [Range(1, 1000)]
    public int CircuitBreakerFailureThreshold { get; set; } = 10;

    [Range(1, 3600)]
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
    
    [Range(0.0, 1.0)]
    public double MaxFailureRatio { get; set; } = 0.25;
}
