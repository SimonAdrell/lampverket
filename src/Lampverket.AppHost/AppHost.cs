var builder = DistributedApplication.CreateBuilder(args);

// External Home Assistant instance — graph visibility only. Not managed by Aspire.
// The actual URL is configured via user-secrets in each consuming project
// (HomeAssistant:BaseUrl), not through this connection string.
// WithReference wires the dependency edge so the dashboard shows it and
// future WaitFor/health-gate support can hook in when the agent project exists.
// var homeAssistant = builder.AddConnectionString("HomeAssistant");

var apiService = builder.AddProject<Projects.Lampverket_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Lampverket_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
