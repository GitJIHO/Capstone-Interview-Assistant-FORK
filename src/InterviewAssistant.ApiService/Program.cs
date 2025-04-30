using System.Text.Json;
using System.Text.Json.Serialization;

using InterviewAssistant.ApiService.Endpoints;
using InterviewAssistant.ApiService.Services;

using Microsoft.SemanticKernel;

using OpenAI;

// 시작 시 Application Insights 연결 상태 확인
var builder = WebApplication.CreateBuilder(args);

// Application Insights 연결 문자열 확인 로깅
var appInsightsConnectionString = builder.Configuration["ApplicationInsights__ConnectionString"] 
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

// 환경 변수에서 직접 확인 (GitHub Actions에서 설정한 경우)
if (string.IsNullOrEmpty(appInsightsConnectionString))
{
    appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
    if (!string.IsNullOrEmpty(appInsightsConnectionString))
    {
        // 구성에 추가 (Extensions.cs에서 참조할 수 있도록)
        builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] = appInsightsConnectionString;
    }
    // 여전히 없다면 마지막 수단으로 하드코딩된 값을 사용
    else
    {
        // 프로덕션 환경에서만 하드코딩된 값을 사용 (보안 목적)
        if (builder.Environment.IsProduction())
        {
            appInsightsConnectionString = "InstrumentationKey=a3a8c178-fa49-467d-a026-8d4b451d8dd8;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=f7dde35b-7fa8-4f2f-867d-233d98b0bc77";
            
            // 환경 변수 및 구성에 추가
            Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", appInsightsConnectionString);
            builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] = appInsightsConnectionString;
            Console.WriteLine("[ApiService] ⚠️ 하드코딩된 백업 연결 문자열 사용 중");
        }
    }
}

Console.WriteLine("====================================================================");
Console.WriteLine($"[ApiService] Application Insights 연결 상태: {(!string.IsNullOrEmpty(appInsightsConnectionString) ? "설정됨" : "설정되지 않음")}");

if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    var maskedConnectionString = appInsightsConnectionString;
    if (maskedConnectionString.Contains("InstrumentationKey="))
    {
        int start = maskedConnectionString.IndexOf("InstrumentationKey=") + "InstrumentationKey=".Length;
        int end = maskedConnectionString.IndexOf(';', start);
        if (end == -1) end = maskedConnectionString.Length;
        
        string key = maskedConnectionString.Substring(start, end - start);
        string maskedKey = key.Length > 8 ? $"{key.Substring(0, 4)}...{key.Substring(key.Length - 4)}" : "****";
        maskedConnectionString = maskedConnectionString.Replace(key, maskedKey);
    }
    
    Console.WriteLine($"[ApiService] 연결 문자열(마스킹됨): {maskedConnectionString}");
    
    // 직접 Application Insights 구성
    builder.Services.AddApplicationInsightsTelemetry(options => 
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}
Console.WriteLine("====================================================================");

// .NET Aspire 기본 설정
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IKernelService, KernelService>();

//OpenAPI 설정
builder.Services.AddOpenApi();

builder.AddAzureOpenAIClient("openai");

builder.Services.AddSingleton<Kernel>(sp =>
{
    var config = builder.Configuration;

    var openAIClient = sp.GetRequiredService<OpenAIClient>();
    var kernel = Kernel.CreateBuilder()
                       .AddOpenAIChatCompletion(
                           modelId: config["GitHub:Models:ModelId"]!,
                           openAIClient: openAIClient,
                           serviceId: "github")
                       .Build();
                       
    return kernel;
});

// JSON 직렬화 설정
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

// 개발 환경에서만 Swagger UI 활성화
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();

// Chat Completion 엔드포인트 매핑
app.MapChatCompletionEndpoint();

// .NET Aspire 헬스체크 및 모니터링 엔드포인트 매핑
app.MapDefaultEndpoints();

app.Run();
