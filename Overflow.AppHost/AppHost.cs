using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

//var compose = builder.AddDockerComposeEnvironment("production")
//    .WithDashboard(dashboard => dashboard.WithHostPort(8080));
//var apiService = builder.AddProject<Projects.Overflow_ApiService>("apiservice")
//    .WithHttpHealthCheck("/health");

var kcPort = builder.ExecutionContext.IsPublishMode ? 80 : 6001;

var keyCloak = builder.AddKeycloak("keycloak",kcPort)
    .WithEndpoint("http", e => e.IsExternal = true)
    .WithDataVolume("keycloak-data")
    //.WithRealmImport("../infa/realms")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEnvironment("KC_PROXY_HEADERS", "xforwarded");
    //.WithEnvironment("VIRTUAL_HOST", "id.overflow.local")
    //.WithEnvironment("VIRTUAL_PORT", "8080");

var pgUser = builder.AddParameter("pg-username", secret:true);
var pgPassword = builder.AddParameter("pg-password", secret:true);

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .WithPasswordAuthentication(pgUser, pgPassword);
    //.WithDataVolume("postgres-data")
    //.WithPgAdmin()
    //.WithPgWeb();

//var typesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

var typesense = builder.AddContainer("typesense", "typesense/typesense","29.0")
    .WithArgs("--data-dir", "/data", "--api-key=xyz", "--enable-cors")
    .WithVolume("typesense-data", "/data")
    .WithHttpEndpoint(8108, 8108, name: "typesense");

var typesenseContainer = typesense.GetEndpoint("typesense");

var questionDb = postgres.AddDatabase("questiondb");
var profileDb = postgres.AddDatabase("profileDb");
var statDb = postgres.AddDatabase("statDb");
var voteDb = postgres.AddDatabase("voteDb");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin(port: 15672);

var questionService = builder.AddProject<Projects.QuestionService>("question-svc")
    .WithReference(keyCloak)
    .WithReference(questionDb)
    .WithReference(rabbitmq)
    .WaitFor(keyCloak)
    .WaitFor(questionDb)
    .WaitFor(rabbitmq);

var profileService = builder.AddProject<Projects.ProfileService>("profile-svc")
    .WithReference(keyCloak)
    .WithReference(profileDb)
    .WithReference(rabbitmq)
    .WaitFor(keyCloak)
    .WaitFor(profileDb)
    .WaitFor(rabbitmq);

var statService = builder.AddProject<Projects.StatsService>("stat-svc")
    .WithReference(statDb)
    .WithReference(rabbitmq)
    .WaitFor(statDb)
    .WaitFor(rabbitmq);

//builder.AddProject<Projects.Overflow_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithHttpHealthCheck("/health")
//    .WithReference(apiService)
//    .WaitFor(apiService);

var searchService = builder.AddProject<Projects.SearchService>("search-svc")
    //.WithEnvironment("typesense-api-key", typesenseApiKey)
    .WithReference(typesenseContainer)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WaitFor(typesense);

var voteService = builder.AddProject<Projects.VoteService>("vote-svc")
    .WithReference(keyCloak)
    .WithReference(voteDb)
    .WithReference(rabbitmq)
    .WaitFor(keyCloak)
    .WaitFor(voteDb)
    .WaitFor(rabbitmq);

var yarp = builder.AddYarp("gateway")
    .WithConfiguration(yarpBuilder =>
    {
        yarpBuilder.AddRoute("/questions/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/test/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/tags/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/search/{**catch-all}", searchService);
         yarpBuilder.AddRoute("/profiles/{**catch-all}", profileService);
        yarpBuilder.AddRoute("/stats/{**catch-all}", statService);
        yarpBuilder.AddRoute("/votes/{**catch-all}", voteService);
    })
    .WithEnvironment("ASPNETCORE_URLS", "http://*:8001")
    .WithEndpoint(port: 8001, targetPort: 8001, scheme: "http", name: "gateway", isExternal: true);
    //.WithEnvironment("VIRTUAL_HOST", "api.overflow.local")
    //.WithEnvironment("VIRTUAL_PORT", "8001");


var webapp = builder.AddNpmApp("webapp", "../webapp", "dev")
    .WithReference(keyCloak)
    
    //.WithEnvironment("VIRTUAL_HOST", "app.overflow.local")
    //.WithEnvironment("VIRTUAL_PORT", "4000")
    .PublishAsDockerFile();

//if (!builder.Environment.IsDevelopment())
//{
//    builder.AddContainer("nginx-proxy", "nginxproxy/nginx-proxy", "1.8")
//        .WithEndpoint(80, 80, "nginx", isExternal: true)
//        .WithEndpoint(443, 443, "nginx-ssl", isExternal: true)
//        .WithBindMount("/var/run/docker.sock", "/tmp/docker.sock", true)
//        .WithBindMount("../infa/devcerts", "/etc/nginx/certs", true);
//}

if (builder.ExecutionContext.IsPublishMode)
{
    rabbitmq.WithVolume("rabbitmq-data", "/var/lib/rabbitmq/mnesia");
    webapp.WithEndpoint(env: "PORT", port: 80, targetPort: 4000, scheme: "http", isExternal: true);
}
else
{
    postgres.RunAsContainer();
    rabbitmq.WithDataVolume("rabbitmq-data");
    webapp.WithHttpEndpoint(env: "PORT", port: 3000, targetPort: 4000);
}


    builder.Build().Run();
