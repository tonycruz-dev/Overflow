var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Overflow_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

var keyCloak = builder.AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data");




builder.AddProject<Projects.Overflow_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
