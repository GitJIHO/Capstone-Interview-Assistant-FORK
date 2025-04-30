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

Console.WriteLine("====================================================================");
Console.WriteLine($"Application Insights 연결 상태: {(!string.IsNullOrEmpty(appInsightsConnectionString) ? "설정됨" : "설정되지 않음")}");

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
    
    Console.WriteLine($"연결 문자열(마스킹됨): {maskedConnectionString}");
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
