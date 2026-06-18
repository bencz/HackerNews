using HackerNews.Application.Abstractions;
using HackerNews.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace HackerNews.UnitTests.Infrastructure.Health;

[TestFixture]
public class SnapshotReadyHealthCheckTests
{
    [Test]
    public async Task CheckHealthAsync_Ready_ReturnsHealthy()
    {
        var reader = Substitute.For<IBestStoriesCacheReader>();
        reader.IsReady.Returns(true);
        var check = new SnapshotReadyHealthCheck(reader);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task CheckHealthAsync_NotReady_ReturnsUnhealthy()
    {
        var reader = Substitute.For<IBestStoriesCacheReader>();
        reader.IsReady.Returns(false);
        var check = new SnapshotReadyHealthCheck(reader);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
            Assert.That(result.Description, Is.EqualTo("Snapshot not yet populated"));
        });
    }
}
