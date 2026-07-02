var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Lampverket_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
