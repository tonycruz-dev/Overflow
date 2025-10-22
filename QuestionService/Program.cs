using Common;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using QuestionService.Data;
using QuestionService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TagService>();

builder.Services.AddKeyCloakAuthentication();

//builder.Services.AddAuthentication()
//    .AddKeycloakJwtBearer(serviceName: "keycloak", realm: "overflow", options =>
//    { 
//      options.RequireHttpsMetadata = false;
//      options.Audience = "overflow";
//    });

builder.AddNpgsqlDbContext<QuestionDbContext>("questiondb");

//builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
//{
//    traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
//        .AddService(builder.Environment.ApplicationName))
//        .AddSource("Wolverine");
//});

//var retryPolicy = Policy
//    .Handle<BrokerUnreachableException>()
//    .Or<SocketException>()
//    .WaitAndRetryAsync(
//    retryCount: 5,retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
//    (exception, timeSpan,retryCount) =>
//    {
//        Console.WriteLine($"Retry attempt {retryCount} failed. Retrying in  {timeSpan.Seconds} seconds.");
//    });

//await retryPolicy.ExecuteAsync(async () =>
//{
//    var endpont = builder.Configuration.GetConnectionString("messaging") 
//    ?? throw new InvalidOperationException("messaging connection string not found.");

//    var factory = new ConnectionFactory()
//    {
//        Uri = new Uri(endpont)
//    };

//    await using var connection = await factory.CreateConnectionAsync();
//});

await builder.UseWolverineWithRabbitMqAsync(opts =>
{
    opts.PublishAllMessages().ToRabbitExchange("questions");
    opts.ApplicationAssembly = typeof(Program).Assembly;
});


//builder.Host.UseWolverine(opts =>
//{
//    opts.UseRabbitMqUsingNamedConnection("messaging").AutoProvision();
//    // Publish messages to the 'questions' exchange (fan-out) so multiple consumers can bind
//    opts.PublishAllMessages().ToRabbitExchange("questions");
//});


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF Core migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuestionDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

await app.RunAsync();
