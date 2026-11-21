using System.Text.Json;
using System.Text.Json.Serialization;
using COA.Api.Authentication;
using COA.Api.Profile;
using COA.Api.Opportunity;
using COA.Application.Matching;
using COA.Application.Profile;
using COA.Application.Opportunity;
using COA.Infrastructure.ProfilePersistence;
using COA.Infrastructure.OpportunityPersistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("COA");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:COA must be configured through a secure environment or secret store.");
}

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.AllowDuplicateProperties = false;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
});
builder.Services.AddDbContextFactory<CoaDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsAssembly(typeof(CoaDbContext).Assembly.GetName().Name));
});
builder.Services.AddScoped<IProfileStore, SqlServerProfileStore>();
builder.Services.AddScoped<ProfileUseCases>();
builder.Services.AddScoped<IOpportunityStore, SqlServerOpportunityStore>();
builder.Services.AddScoped<OpportunityUseCases>();
builder.Services.AddApiJwtBearerAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler(exceptionApp => exceptionApp.Run(context =>
    Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "An unexpected error occurred",
        type: "https://httpstatuses.com/500").ExecuteAsync(context)));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/me", (HttpContext context) =>
{
    if (!AuthenticatedOwner.TryCreate(context.User, out var owner))
        return ForbiddenProblem();

    return Results.Ok(new CurrentIdentityResponse(owner.Issuer, owner.Subject));
}).RequireAuthorization();

var profile = app.MapGroup("/api/profile").RequireAuthorization();
var opportunities = app.MapGroup("/api/opportunities").RequireAuthorization();
opportunities.MapPost("", async (HttpContext context, OpportunityUseCases useCases, OpportunityImportRequest request, CancellationToken ct) =>
{
    if (!AuthenticatedOwner.TryCreateOwnerIdentity(context.User, out var owner)) return ForbiddenProblem();
    try { var result=await useCases.ImportAsync(owner,request.Url,request.Title,request.Description,ct); return result.Created ? Results.Created("/api/opportunities", new { result.Opportunity, result.Assessment }) : ConflictProblem(); }
    catch (ArgumentException) { return BadRequestProblem(); }
});
opportunities.MapGet("", async (HttpContext context, OpportunityUseCases useCases, CancellationToken ct) =>
{ if (!AuthenticatedOwner.TryCreateOwnerIdentity(context.User,out var owner)) return ForbiddenProblem(); return Results.Ok((await useCases.ListScoredAsync(owner,ct)).Select(x => new { x.Opportunity, x.Assessment })); });

profile.MapGet("", async (HttpContext context, ProfileUseCases useCases, CancellationToken cancellationToken) =>
{
    if (!AuthenticatedOwner.TryCreateOwnerIdentity(context.User, out var owner))
        return ForbiddenProblem();

    var result = await useCases.GetCurrentAsync(owner, cancellationToken);
    return result switch
    {
        ProfileReadResult.Found found => Results.Ok(ProfileHttpResponse.FromDomain(found.Profile)),
        ProfileReadResult.NotFound => NotFoundProblem(),
        _ => UnexpectedResult()
    };
});

profile.MapPost("", async (HttpContext context, ProfileUseCases useCases, CancellationToken cancellationToken) =>
{
    if (!AuthenticatedOwner.TryCreateOwnerIdentity(context.User, out var owner))
        return ForbiddenProblem();

    var (request, error) = await ReadRequestAsync<CreateProfileHttpRequest>(context.Request, cancellationToken);
    if (error is not null)
        return error;

    try
    {
        var result = await useCases.CreateAsync(owner, request!.ToApplication(), cancellationToken);
        return result switch
        {
            CreateProfileResult.Created created => Results.Created(
                "/api/profile",
                ProfileHttpResponse.FromDomain(created.Profile)),
            CreateProfileResult.Conflict => ConflictProblem(),
            _ => UnexpectedResult()
        };
    }
    catch (Exception exception) when (exception is ArgumentException or OverflowException)
    {
        return BadRequestProblem();
    }
});

profile.MapPut("", async (HttpContext context, ProfileUseCases useCases, CancellationToken cancellationToken) =>
{
    if (!AuthenticatedOwner.TryCreateOwnerIdentity(context.User, out var owner))
        return ForbiddenProblem();

    var (request, error) = await ReadRequestAsync<ReviseProfileHttpRequest>(context.Request, cancellationToken);
    if (error is not null)
        return error;

    try
    {
        var result = await useCases.ReviseAsync(owner, request!.ToReviseApplication(), cancellationToken);
        return result switch
        {
            ReviseProfileResult.Revised revised => Results.Ok(ProfileHttpResponse.FromDomain(revised.Profile)),
            ReviseProfileResult.NotFound => NotFoundProblem(),
            ReviseProfileResult.Conflict => ConflictProblem(),
            _ => UnexpectedResult()
        };
    }
    catch (Exception exception) when (exception is ArgumentException or OverflowException)
    {
        return BadRequestProblem();
    }
});

profile.MapGet("/versions/{version:int}", async (
    HttpContext context,
    int version,
    ProfileUseCases useCases,
    CancellationToken cancellationToken) =>
{
    if (version < 1)
        return BadRequestProblem();
    if (!AuthenticatedOwner.TryCreateOwnerIdentity(context.User, out var owner))
        return ForbiddenProblem();

    var result = await useCases.GetVersionAsync(owner, version, cancellationToken);
    return result switch
    {
        ProfileReadResult.Found found => Results.Ok(ProfileHttpResponse.FromDomain(found.Profile)),
        ProfileReadResult.NotFound => NotFoundProblem(),
        _ => UnexpectedResult()
    };
});

if (app.Environment.IsDevelopment())
{
    // Development-only synthetic bootstrap; it has no persistence or owner context.
    app.MapPost("/api/matching/evaluate", (EvaluateMatchRequest? request) =>
    {
        if (request?.Criteria is null)
            return BadRequestProblem();

        try
        {
            return Results.Ok(EvaluateMatch.Execute(request));
        }
        catch (ArgumentException)
        {
            return BadRequestProblem();
        }
    });
}

app.Run();

static async Task<(T? Request, IResult? Error)> ReadRequestAsync<T>(HttpRequest request, CancellationToken cancellationToken)
{
    try
    {
        var body = await request.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return body is null ? (default, BadRequestProblem()) : (body, null);
    }
    catch (Exception exception) when (exception is JsonException or NotSupportedException or BadHttpRequestException)
    {
        return (default, BadRequestProblem());
    }
}

static IResult BadRequestProblem() => Results.Problem(
    statusCode: StatusCodes.Status400BadRequest,
    title: "Invalid profile request",
    type: "https://httpstatuses.com/400");

static IResult ForbiddenProblem() => Results.Problem(
    statusCode: StatusCodes.Status403Forbidden,
    title: "Profile identity is not valid",
    type: "https://httpstatuses.com/403");

static IResult NotFoundProblem() => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "Profile not found",
    type: "https://httpstatuses.com/404");

static IResult ConflictProblem() => Results.Problem(
    statusCode: StatusCodes.Status409Conflict,
    title: "Profile version conflict",
    type: "https://httpstatuses.com/409");

static IResult UnexpectedResult() => Results.Problem(
    statusCode: StatusCodes.Status500InternalServerError,
    title: "An unexpected error occurred",
    type: "https://httpstatuses.com/500");

public partial class Program;
