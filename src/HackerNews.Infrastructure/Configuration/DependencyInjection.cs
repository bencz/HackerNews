using HackerNews.Application.Abstractions;
using HackerNews.Application.Configuration;
using HackerNews.Infrastructure.Clients.HackerNews;
using HackerNews.Infrastructure.Configuration.Http;
using HackerNews.Infrastructure.Health;
using HackerNews.Infrastructure.HostedServices;
using HackerNews.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HackerNews.Infrastructure.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(HackerNewsOptions.SectionName)
            .Get<HackerNewsOptions>() ?? new HackerNewsOptions();

        services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            })
            .AddPolicyHandler(ResiliencePolicies.Retry(options))
            .AddPolicyHandler(ResiliencePolicies.CircuitBreaker(options))
            .AddPolicyHandler(ResiliencePolicies.Timeout(options));

        AddDistributedCache(services, configuration);

        services.AddHostedService<BestStoriesRefreshService>();

        return services;
    }

    private static void AddDistributedCache(IServiceCollection services, IConfiguration configuration)
    {
        var redisOptions = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? new RedisOptions();

        if (!redisOptions.Enabled)
        {
            services.AddSingleton<ISnapshotStore, NoOpSnapshotStore>();
            services.AddSingleton<ISnapshotChannel, NoOpSnapshotChannel>();
            services.AddSingleton<IDistributedLock, NoOpDistributedLock>();
            return;
        }

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            config.AbortOnConnectFail = false;
            config.ClientName = "hackernews-api";
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<ISnapshotStore, RedisSnapshotStore>();
        services.AddSingleton<ISnapshotChannel, RedisSnapshotChannel>();
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        services.AddHostedService<SnapshotSubscriberService>();
    }
    
    public static IHealthChecksBuilder AddSnapshotReadyCheck(
        this IHealthChecksBuilder builder,
        string name = "snapshot",
        params string[] tags)
    {
        return builder.AddCheck<SnapshotReadyHealthCheck>(name, tags: tags);
    }
}
