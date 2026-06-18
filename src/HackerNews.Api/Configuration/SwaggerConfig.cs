using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;

namespace HackerNews.Api.Configuration;

public static class SwaggerConfig
{
    public static void AddSwaggerConfiguration(this IServiceCollection services, IWebHostEnvironment environment)
    {
        if (!AllowSwagger(environment))
            return;
        
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Hacker News API",
                Version = "v1"
            });
        });
    }

    public static void UseSwaggerConfiguration(this WebApplication app, IWebHostEnvironment environment)
    {
        if (!AllowSwagger(environment))
            return;
        
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
            }
        });
    }

    private static bool AllowSwagger(IWebHostEnvironment environment) 
        => environment.IsDevelopment() || environment.IsStaging();
}
