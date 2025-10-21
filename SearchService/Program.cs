using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SearchService.Data;
using SearchService.Models;
using System.Text.RegularExpressions;
using Typesense;
using Typesense.Setup;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
var typesenseUri = builder.Configuration["services:typesense:typesense:0"];
if(string.IsNullOrEmpty(typesenseUri))
{
    throw new InvalidOperationException("Typesense service endpoint is not configured.");
}
var typesenseApiKey = builder.Configuration["typesense-api-key"];

builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
{
    traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(builder.Environment.ApplicationName))
        .AddSource("Wolverine");
});

builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMqUsingNamedConnection("messaging").AutoProvision();

    // Listen to a queue bound to the 'questions' exchange to receive events
    opts.ListenToRabbitQueue("questions.search", cfg =>
    {
        cfg.BindExchange("questions");
    });

    // Explicitly connect message types to handler classes if assembly scanning misses them
    opts.Policies.AutoApplyTransactions();
});


//if (string.IsNullOrEmpty(typesenseApiKey))
//{
//    throw new InvalidOperationException("Typesense API key is not configured.");
//}

var uri = new Uri(typesenseUri);
builder.Services.AddTypesenseClient(config =>
{
    config.ApiKey = "xyz"; //typesenseApiKey;
    config.Nodes =
    [
        new (uri.Host, uri.Port.ToString(), uri.Scheme)
    ];
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseAuthorization();

//app.MapControllers();
app.MapDefaultEndpoints();

app.MapGet("/search", async (string query, ITypesenseClient client) =>
{
    // [aspire]something
    string? tag = null;
    var tagMatch = Regex.Match(query, @"\[(.*?)\]");
    if (tagMatch.Success)
    {
        tag = tagMatch.Groups[1].Value;
        query = query.Replace(tagMatch.Value, "").Trim();
    }

    var searchParams = new SearchParameters(query, "title,content");

    if (!string.IsNullOrWhiteSpace(tag))
    {
        searchParams.FilterBy = $"tags:=[{tag}]";
    }

    try
    {
        var result = await client.Search<SearchQuestion>("questions", searchParams);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception e)
    {
        return Results.Problem("Typesense search failed", e.Message);
    }
});

app.MapGet("/search/similar-titles", async (string query, ITypesenseClient client) =>
{
    var searchParams = new SearchParameters(query, "title");

    try
    {
        var result = await client.Search<SearchQuestion>("questions", searchParams);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception e)
    {
        return Results.Problem("Typesense search failed", e.Message);
    }
});

using var scope = app.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<ITypesenseClient>();
await SearchInitializer.EnsureIndexExists(client);

app.Run();
