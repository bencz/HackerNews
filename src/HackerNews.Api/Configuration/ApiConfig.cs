using Asp.Versioning;
using HackerNews.Api.Configuration.Conventions;
using HackerNews.Api.Configuration.Middlewares;
using HackerNews.Infrastructure.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace HackerNews.Api.Configuration;

public static class ApiConfig
{
    private const string ApplicationName = "Hacker News API";
    
    public static void AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options =>
        {
            options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));
        });

        services.AddHttpContextAccessor();
        services.AddHttpClient();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy("Total",
                builder =>
                {
                    if (allowedOrigins.Length > 0)
                        builder.WithOrigins(allowedOrigins);
                    else
                        builder.AllowAnyOrigin();

                    builder
                        .WithMethods("GET")
                        .AllowAnyHeader();
                });
        });
        
        services.AddApiVersioning(p =>
        {
            p.DefaultApiVersion = new ApiVersion(1, 0);
            p.ReportApiVersions = true;
            p.AssumeDefaultVersionWhenUnspecified = true;
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });
    }

    public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddSnapshotReadyCheck(tags: ["ready"]);
        
        return services;
    }
    
    private static WebApplication ConfigureMiddlewares(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggerMiddleware>();
        app.UseMiddleware<ExceptionMiddleware>();

        return app;
    }

    private static WebApplication MapHealthChecks(this WebApplication app)
    {
        app.UseHealthChecks("/health/startup");
        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready")
        });

        return app;
    }
    
    public static WebApplication UseApiConfiguration(this WebApplication app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment()) 
            app.UseDeveloperExceptionPage();
        
        // app.UseHttpsRedirection();
        app.ConfigureMiddlewares();
        app.UseSwaggerConfiguration(env);
        app.UseRouting();
        app.UseCors("Total");
        app.MapControllers();
        app.MapHealthChecks();

        return app;
    }
    
    public static void RunApplication(this WebApplication app)
    {
        try
        {
            app.Logger.LogInformation("Starting web host ({ApplicationName})...", ApplicationName);
            app.Run();
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Host terminated unexpectedly ({ApplicationName})...", ApplicationName);
            throw;
        }
        finally
        {
            Serilog.Log.CloseAndFlush();
        }
    }
}