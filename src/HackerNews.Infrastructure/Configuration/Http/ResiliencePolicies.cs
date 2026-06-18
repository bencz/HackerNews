using System.Net;
using HackerNews.Application.Configuration;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace HackerNews.Infrastructure.Configuration.Http;

internal static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> Retry(HackerNewsOptions options)
    {
        var delays = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: TimeSpan.FromMilliseconds(options.RetryMedianFirstDelayMs),
            retryCount: options.RetryCount);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(delays);
    }

    public static IAsyncPolicy<HttpResponseMessage> CircuitBreaker(HackerNewsOptions options)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: options.CircuitBreakerFailureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds));
    }

    public static IAsyncPolicy<HttpResponseMessage> Timeout(HackerNewsOptions options)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(options.HttpTimeoutSeconds),
            TimeoutStrategy.Optimistic);
    }
}
