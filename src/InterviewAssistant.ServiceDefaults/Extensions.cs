using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;
using System.Diagnostics;

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

        // Enable Azure Monitor exporter for Application Insights
        var appInsightsConnectionString = builder.Configuration["ApplicationInsights__ConnectionString"] 
            ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        
        // 연결 문자열 로깅을 위한 ILogger 가져오기
        var loggerFactory = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("ApplicationInsightsConfiguration");
        
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            // 마스킹된 연결 문자열 로깅 (보안을 위해)
            string maskedConnectionString = MaskConnectionString(appInsightsConnectionString);
            logger?.LogInformation("Application Insights 연결 중: {MaskedConnectionString}", maskedConnectionString);
            Debug.WriteLine($"Application Insights 연결 문자열: {maskedConnectionString}");

            try
            {
                // Application Insights를 직접 추가하여 전체 기능 활성화
                builder.Services.AddApplicationInsightsTelemetry(options => 
                {
                    options.ConnectionString = appInsightsConnectionString;
                });
                
                // OpenTelemetry 익스포터도 사용 (두 가지 방식 모두 활성화)
                builder.Services.AddOpenTelemetry()
                    .UseAzureMonitor(options =>
                    {
                        options.ConnectionString = appInsightsConnectionString;
                    });
                
                logger?.LogInformation("Application Insights 구성 완료");
                Debug.WriteLine("Application Insights 구성 완료");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Application Insights 구성 중 오류 발생");
                Debug.WriteLine($"Application Insights 오류: {ex.Message}");
            }
        }
        else
        {
            logger?.LogWarning("Application Insights 연결 문자열을 찾을 수 없습니다");
            Debug.WriteLine("Application Insights 연결 문자열 없음");
        }

        return builder;
    }

    // 보안을 위해 연결 문자열 마스킹
    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "[비어있음]";

        try
        {
            // InstrumentationKey 부분 마스킹
            if (connectionString.Contains("InstrumentationKey="))
            {
                int start = connectionString.IndexOf("InstrumentationKey=") + "InstrumentationKey=".Length;
                int end = connectionString.IndexOf(';', start);
                if (end == -1) end = connectionString.Length;
                
                string key = connectionString.Substring(start, end - start);
                string maskedKey = key.Length > 8 
                    ? $"{key.Substring(0, 4)}...{key.Substring(key.Length - 4)}"
                    : "****";
                
                return connectionString.Replace(key, maskedKey);
            }
            return "[마스킹된 연결 문자열]";
        }
        catch
        {
            return "[마스킹 실패]";
        }
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
