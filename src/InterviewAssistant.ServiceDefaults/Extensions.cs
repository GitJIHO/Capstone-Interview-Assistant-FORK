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
            
        // 환경 변수에서도 직접 확인 (Program.cs에서 설정한 경우)
        if (string.IsNullOrEmpty(appInsightsConnectionString))
        {
            appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        }
        
        // 하드코딩된 백업 연결 문자열 (최후의 수단으로 시도)
        if (string.IsNullOrEmpty(appInsightsConnectionString) && !builder.Environment.IsDevelopment())
        {
            appInsightsConnectionString = "InstrumentationKey=a3a8c178-fa49-467d-a026-8d4b451d8dd8;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=f7dde35b-7fa8-4f2f-867d-233d98b0bc77";
        }
        
        // 안전한 로깅 방식으로 변경 (BuildServiceProvider 제거)
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            // 마스킹된 연결 문자열 로깅 (보안을 위해)
            string maskedConnectionString = MaskConnectionString(appInsightsConnectionString);
            Debug.WriteLine($"[ServiceDefaults] Application Insights 연결 문자열: {maskedConnectionString}");
            
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
                
                Debug.WriteLine("[ServiceDefaults] Application Insights 구성 완료");
                
                // 로거를 직접 가져오는 대신, 시작할 때 로그를 작성할 수 있도록 로깅 콜백 등록
                builder.Services.AddHostedService<AppInsightsStartupLogger>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ServiceDefaults] Application Insights 오류: {ex.Message}");
                // 예외를 삼키지만 디버그 로그는 남김
            }
        }
        else
        {
            Debug.WriteLine("[ServiceDefaults] Application Insights 연결 문자열 없음");
            // 애플리케이션 시작 시 경고 로그를 남기는 서비스 등록
            builder.Services.AddHostedService<AppInsightsWarningLogger>();
        }

        return builder;
    }
    
    // 시작 시 Application Insights 구성 성공을 로깅하는 백그라운드 서비스
    private class AppInsightsStartupLogger : BackgroundService
    {
        private readonly ILogger<AppInsightsStartupLogger> _logger;

        public AppInsightsStartupLogger(ILogger<AppInsightsStartupLogger> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ServiceDefaults] Application Insights가 성공적으로 구성되었습니다.");
            return Task.CompletedTask;
        }
    }
    
    // 시작 시 Application Insights 연결 문자열 누락 경고를 로깅하는 백그라운드 서비스
    private class AppInsightsWarningLogger : BackgroundService
    {
        private readonly ILogger<AppInsightsWarningLogger> _logger;

        public AppInsightsWarningLogger(ILogger<AppInsightsWarningLogger> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogWarning("[ServiceDefaults] Application Insights 연결 문자열이 누락되었습니다. 텔레메트리가 전송되지 않을 수 있습니다.");
            return Task.CompletedTask;
        }
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
