using COA.Application.Matching;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// MVP synthetic-data vertical slice; this endpoint does not persist or submit applications.
app.MapPost("/api/matching/evaluate", (EvaluateMatchRequest? request) =>
{
    if (request is null || request.Criteria is null)
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
