using Common;
using Microsoft.EntityFrameworkCore;
using ProfileService.Data;
using ProfileService.DTOs;
using ProfileService.Middleware;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddKeyCloakAuthentication();
await builder.UseWolverineWithRabbitMqAsync(opts =>
{
    opts.ApplicationAssembly = typeof(Program).Assembly;
});
builder.AddNpgsqlDbContext<ProfileDbContext>("profileDb");

var app = builder.Build();

//app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<UserProfileCreationMiddleware>();

app.MapGet("/profiles/me", async (ClaimsPrincipal user, ProfileDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var profile = await db.UserProfiles.FindAsync(userId);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
}).RequireAuthorization();

app.MapGet("/profiles/batch", async (string ids, ProfileDbContext db) =>
{
    var list = ids.Split(",", StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

    var rows = await db.UserProfiles
        .Where(x => list.Contains(x.Id))
        .Select(x => new ProfileSummaryDto(x.Id, x.DisplayName, x.Reputation))
        .ToListAsync();

    return Results.Ok(rows);
});

app.MapGet("profiles", async (string? sortBy, ProfileDbContext db) =>
{
    var query = db.UserProfiles.AsQueryable();

    query = sortBy == "reputation"
        ? query.OrderByDescending(x => x.Reputation)
        : query.OrderBy(x => x.DisplayName);

    return await query.ToListAsync();
});

app.MapGet("profiles/{id}", async (string id, ProfileDbContext db) =>
{
    var profile = await db.UserProfiles.FindAsync(id);

    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.MapPut("/profiles/edit", async (EditProfileDto dto, ClaimsPrincipal user, ProfileDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile is null) return Results.NotFound();

    profile.DisplayName = dto.DisplayName ?? profile.DisplayName;
    profile.Description = dto.Description ?? profile.Description;

    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization();

//using var scope = app.Services.CreateScope();
//var services = scope.ServiceProvider;
//try
//{
//    var context = services.GetRequiredService<ProfileDbContext>();
//    await context.Database.MigrateAsync();
//}
//catch (Exception e)
//{
//    var logger = services.GetRequiredService<ILogger<Program>>();
//    logger.LogError(e, "An error occurred while migrating or seeding the database.");
//}
 await app.MigrateDbContextAsync<ProfileDbContext>();
app.Run();

