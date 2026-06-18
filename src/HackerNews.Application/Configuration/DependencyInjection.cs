using HackerNews.Application.Abstractions;
using HackerNews.Application.Cache;
using HackerNews.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.Application.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<HackerNewsOptions>()
            .Bind(configuration.GetSection(HackerNewsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<BestStoriesCache>();
        services.AddSingleton<IBestStoriesCacheReader>(sp => sp.GetRequiredService<BestStoriesCache>());
        services.AddSingleton<IBestStoriesCacheWriter>(sp => sp.GetRequiredService<BestStoriesCache>());

        services.AddScoped<IBestStoriesService, BestStoriesService>();

        return services;
    }
}