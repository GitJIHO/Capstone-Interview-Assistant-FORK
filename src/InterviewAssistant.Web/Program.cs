using InterviewAssistant.Web.Components;
using InterviewAssistant.Web.Services;
using InterviewAssistant.Web.Clients;

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
}

Console.WriteLine("====================================================================");
Console.WriteLine($"[WebFrontend] Application Insights 연결 상태: {(!string.IsNullOrEmpty(appInsightsConnectionString) ? "설정됨" : "설정되지 않음")}");

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
    
    Console.WriteLine($"[WebFrontend] 연결 문자열(마스킹됨): {maskedConnectionString}");
    
    // 직접 Application Insights 구성
    builder.Services.AddApplicationInsightsTelemetry(options => 
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}
Console.WriteLine("====================================================================");

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

// ChatApiClient 등록
builder.Services.AddHttpClient<IChatApiClient, ChatApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
});

// ChatService 등록
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();