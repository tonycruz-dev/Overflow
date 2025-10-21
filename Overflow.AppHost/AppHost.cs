var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Overflow_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

var keyCloak = builder.AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data");

var postgres = builder.AddPostgres("postgres", port: 5432)
    .WithDataVolume("postgres-data")
    .WithPgAdmin();

var questionDb = postgres.AddDatabase("questiondb");

var questionService = builder.AddProject<Projects.QuestionService>("question-svc")
    .WithReference(keyCloak)
    .WithReference(questionDb)
    .WaitFor(keyCloak)
    .WaitFor(questionDb);






builder.AddProject<Projects.Overflow_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

//builder.AddProject<Projects.QuestionService>("questionservice");

builder.Build().Run();
