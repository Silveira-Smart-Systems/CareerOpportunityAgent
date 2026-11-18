using System.Text.Json.Serialization;
using COA.Application.Matching;
using COA.Api.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApiJwtBearerAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/me", (HttpContext context) =>
{
    if (!AuthenticatedOwner.TryCreate(context.User, out var owner))
        return Results.Forbid();

    return Results.Ok(new CurrentIdentityResponse(owner.Issuer, owner.Subject));
}).RequireAuthorization();

// Synthetic-data bootstrap only. This endpoint does not persist or submit applications.
app.MapPost("/api/matching/evaluate", (EvaluateMatchRequest? request) =>
{
    if (request?.Criteria is null)
        return Results.BadRequest(new { error = "Criteria are required." });

    try
    {
        return Results.Ok(EvaluateMatch.Execute(request));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public partial class Program;
