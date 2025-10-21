var builder = DistributedApplication.CreateBuilder(args);

//var apiService = builder.AddProject<Projects.Overflow_ApiService>("apiservice")
//    .WithHttpHealthCheck("/health");

var keyCloak = builder.AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data");

var postgres = builder.AddPostgres("postgres", port: 5432)
    .WithDataVolume("postgres-data")
    .WithPgAdmin();

//var typesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

var typesense = builder.AddContainer("typesense", "typesense/typesense","29.0")
    .WithArgs("--data-dir", "/data", "--api-key=xyz", "--enable-cors")
    .WithVolume("typesense-data", "/data")
    .WithHttpEndpoint(8108, 8108, name: "typesense");

var typesenseContainer = typesense.GetEndpoint("typesense");

var questionDb = postgres.AddDatabase("questiondb");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume("rabbitmq-data")
    .WithManagementPlugin(port: 15672);

var questionService = builder.AddProject<Projects.QuestionService>("question-svc")
    .WithReference(keyCloak)
    .WithReference(questionDb)
    .WithReference(rabbitmq)
    .WaitFor(keyCloak)
    .WaitFor(questionDb)
    .WaitFor(rabbitmq);

//builder.AddProject<Projects.Overflow_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithHttpHealthCheck("/health")
//    .WithReference(apiService)
//    .WaitFor(apiService);

builder.AddProject<Projects.SearchService>("search-svc")
    //.WithEnvironment("typesense-api-key", typesenseApiKey)
    .WithReference(typesenseContainer)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WaitFor(typesense);

builder.Build().Run();
