using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // 모든 가능한 환경 변수 형식 확인
        Console.WriteLine("==== Application Insights 연결 상태 확인 (ServiceDefaults) ====");
        var appInsightsFromConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights");
        var appInsightsFromSection = builder.Configuration["ApplicationInsights__ConnectionString"];
        var appInsightsFromEnv = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        var appInsightsFromDirectEnv = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        var appInsightsFromDirectConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__ApplicationInsights");
        
        Console.WriteLine($"ConnectionStrings:ApplicationInsights: {!string.IsNullOrEmpty(appInsightsFromConnectionString)}");
        Console.WriteLine($"ApplicationInsights__ConnectionString: {!string.IsNullOrEmpty(appInsightsFromSection)}");
        Console.WriteLine($"APPLICATIONINSIGHTS_CONNECTION_STRING: {!string.IsNullOrEmpty(appInsightsFromEnv)}");
        Console.WriteLine($"환경변수 APPLICATIONINSIGHTS_CONNECTION_STRING: {!string.IsNullOrEmpty(appInsightsFromDirectEnv)}");
        Console.WriteLine($"환경변수 ConnectionStrings__ApplicationInsights: {!string.IsNullOrEmpty(appInsightsFromDirectConnStr)}");
        
        // 가능한 모든 방법으로 연결 문자열 취득 시도
        var appInsightsConnectionString = appInsightsFromConnectionString 
            ?? appInsightsFromSection
            ?? appInsightsFromEnv
            ?? appInsightsFromDirectEnv
            ?? appInsightsFromDirectConnStr;
        
        Console.WriteLine($"최종 Application Insights 연결 상태: {!string.IsNullOrEmpty(appInsightsConnectionString)}");
        
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            // 마스킹된 연결 문자열 로깅
            string maskedConnectionString = appInsightsConnectionString;
            if (maskedConnectionString.Contains("InstrumentationKey="))
            {
                int start = maskedConnectionString.IndexOf("InstrumentationKey=") + "InstrumentationKey=".Length;
                int end = maskedConnectionString.IndexOf(';', start);
                if (end == -1) end = maskedConnectionString.Length;
                
                string key = maskedConnectionString.Substring(start, end - start);
                string maskedKey = key.Length > 8 
                    ? $"{key.Substring(0, 4)}...{key.Substring(key.Length - 4)}"
                    : "****";
                
                maskedConnectionString = maskedConnectionString.Replace(key, maskedKey);
                Console.WriteLine($"연결 문자열: {maskedConnectionString}");
            }
            
            builder.Services.AddApplicationInsightsTelemetry(options => 
                options.ConnectionString = appInsightsConnectionString);
            
            builder.Services.AddOpenTelemetry()
                .UseAzureMonitor(options => 
                    options.ConnectionString = appInsightsConnectionString);
        }
        Console.WriteLine("==========================================================");

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
