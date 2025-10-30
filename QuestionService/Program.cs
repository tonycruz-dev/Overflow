using Common;
using Contracts;
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
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
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

//builder.AddNpgsqlDbContext<QuestionDbContext>("questiondb");
var connString = builder.Configuration.GetConnectionString("questionDb");

builder.Services.AddDbContext<QuestionDbContext>(options =>
{
    options.UseNpgsql(connString);
}, optionsLifetime: ServiceLifetime.Singleton);

await builder.UseWolverineWithRabbitMqAsync(opts =>
{
    
    opts.ApplicationAssembly = typeof(Program).Assembly;
    opts.PersistMessagesWithPostgresql(connString!);
    opts.UseEntityFrameworkCoreTransactions();
    //opts.PublishAllMessages().ToRabbitExchange("questions");
    opts.PublishMessage<QuestionCreated>().ToRabbitExchange("Contracts.QuestionCreated").UseDurableOutbox();
    opts.PublishMessage<QuestionUpdated>().ToRabbitExchange("Contracts.QuestionUpdated").UseDurableOutbox();
    opts.PublishMessage<QuestionDeleted>().ToRabbitExchange("Contracts.QuestionDeleted").UseDurableOutbox();
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
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<QuestionDbContext>();
//    await db.Database.MigrateAsync();
//}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

await app.MigrateDbContextAsync<QuestionDbContext>();

await app.RunAsync();
