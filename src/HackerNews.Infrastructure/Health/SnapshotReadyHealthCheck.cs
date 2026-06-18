using HackerNews.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HackerNews.Infrastructure.Health;

internal sealed class SnapshotReadyHealthCheck(IBestStoriesCacheReader cacheReader) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = cacheReader.IsReady
            ? HealthCheckResult.Healthy("Snapshot is populated")
            : HealthCheckResult.Unhealthy("Snapshot not yet populated");

        return Task.FromResult(result);
    }
}
