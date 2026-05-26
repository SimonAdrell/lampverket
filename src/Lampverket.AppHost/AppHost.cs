var builder = DistributedApplication.CreateBuilder(args);

// External Home Assistant instance — not managed by Aspire, declared so the
// dependency is visible in the dashboard resource graph and can be referenced
// by whichever project runs the handläggare agent.
// Configure via AppHost user-secrets: ConnectionStrings:HomeAssistant = <base-url>
var homeAssistant = builder.AddConnectionString("HomeAssistant");

var apiService = builder.AddProject<Projects.Lampverket_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(homeAssistant);

builder.AddProject<Projects.Lampverket_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
