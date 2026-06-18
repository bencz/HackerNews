using System.Net;
using HackerNews.Application.Configuration;
using HackerNews.Infrastructure.Configuration.Http;
using Polly.Timeout;

namespace HackerNews.UnitTests.Infrastructure.Http;

[TestFixture]
public class ResiliencePoliciesTests
{
    private static HackerNewsOptions FastRetryOptions() => new()
    {
        RetryCount = 3,
        RetryMedianFirstDelayMs = 1
    };

    [Test]
    public async Task Retry_TransientError_RetriesUntilSuccess()
    {
        var policy = ResiliencePolicies.Retry(FastRetryOptions());
        var attempts = 0;

        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;
            var status = attempts < 2 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        });

        Assert.Multiple(() =>
        {
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task Retry_TooManyRequests_IsRetried()
    {
        var policy = ResiliencePolicies.Retry(FastRetryOptions());
        var attempts = 0;

        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;
            var status = attempts < 2 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        });

        Assert.Multiple(() =>
        {
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task Retry_Success_DoesNotRetry()
    {
        var policy = ResiliencePolicies.Retry(FastRetryOptions());
        var attempts = 0;

        await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public void Timeout_SlowCall_ThrowsTimeoutRejected()
    {
        var policy = ResiliencePolicies.Timeout(new HackerNewsOptions { HttpTimeoutSeconds = 1 });

        Assert.ThrowsAsync<TimeoutRejectedException>(() => policy.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, CancellationToken.None));
    }
}
