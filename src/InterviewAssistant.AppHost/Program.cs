using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// 테스트 환경인지 확인
var isTestEnvironment = builder.Configuration.GetValue<bool>("IsTestEnvironment", false);
var enableAzureMonitoring = builder.Configuration.GetValue<bool>("EnableAzureMonitoring", !isTestEnvironment);

// ConnectionString 추가
var openai = builder.AddConnectionString("openai");
var config = builder.Configuration;

// 프로젝트 참조 생성
var apiServiceBuilder = builder.AddProject<Projects.InterviewAssistant_ApiService>("apiservice")
                    .WithReference(openai)
                    .WithEnvironment("SemanticKernel__ServiceId", config["SemanticKernel:ServiceId"]!)
                    .WithEnvironment("GitHub__Models__ModelId", config["GitHub:Models:ModelId"]!);

var webFrontendBuilder = builder.AddProject<Projects.InterviewAssistant_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiServiceBuilder)
    .WaitFor(apiServiceBuilder);

// Application Insights는 테스트 환경이 아닐 때만 추가
if (enableAzureMonitoring)
{
    var insights = builder.AddAzureApplicationInsights("applicationinsights");
    apiServiceBuilder.WithReference(insights);
    webFrontendBuilder.WithReference(insights);
    Console.WriteLine("✅ Application Insights enabled for production environment");
}
else
{
    Console.WriteLine("📊 Application Insights disabled for test environment");
}

builder.Build().Run();
