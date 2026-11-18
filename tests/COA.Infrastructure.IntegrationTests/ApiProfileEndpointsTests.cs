using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace COA.Infrastructure.IntegrationTests;

public sealed class ApiProfileEndpointsTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Missing_or_invalid_bearer_tokens_return_401_problem_details()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var missing = await client.GetAsync("/api/profile");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken("owner", signingKey: ApiProfileFactory.OtherSigningKey));
        var invalidSignature = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidSignature.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Validated_owner_can_create_read_history_and_fully_replace_profile()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authorize(client, factory.CreateToken("round-trip"));

        var create = await SendJsonAsync(client, HttpMethod.Post, "/api/profile", ProfileJson("Engineer", expectedVersion: null));
        var current = await client.GetAsync("/api/profile");
        var revise = await SendJsonAsync(client, HttpMethod.Put, "/api/profile", ProfileJson("Architect", expectedVersion: 1));
        var historical = await client.GetAsync("/api/profile/versions/1");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revise.StatusCode);
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        Assert.Equal("Architect", (await JsonDocument.ParseAsync(await revise.Content.ReadAsStreamAsync())).RootElement.GetProperty("headline").GetString());
        Assert.Equal("Engineer", (await JsonDocument.ParseAsync(await historical.Content.ReadAsStreamAsync())).RootElement.GetProperty("headline").GetString());
    }

    [Fact]
    public async Task Other_owner_and_missing_profile_have_the_same_not_found_response()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var ownerA = factory.CreateClient();
        Authorize(ownerA, factory.CreateToken("owner-a"));
        Assert.Equal(HttpStatusCode.Created, (await SendJsonAsync(ownerA, HttpMethod.Post, "/api/profile", ProfileJson("Private", null))).StatusCode);

        using var ownerB = factory.CreateClient();
        Authorize(ownerB, factory.CreateToken("owner-b"));
        var otherOwner = await ownerB.GetAsync("/api/profile");

        using var absentOwner = factory.CreateClient();
        Authorize(absentOwner, factory.CreateToken("owner-c"));
        var absent = await absentOwner.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.NotFound, otherOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        using var otherOwnerProblem = JsonDocument.Parse(await otherOwner.Content.ReadAsStringAsync());
        using var absentProblem = JsonDocument.Parse(await absent.Content.ReadAsStringAsync());
        Assert.Equal(otherOwnerProblem.RootElement.GetProperty("status").GetInt32(), absentProblem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(otherOwnerProblem.RootElement.GetProperty("title").GetString(), absentProblem.RootElement.GetProperty("title").GetString());
        Assert.Equal(otherOwnerProblem.RootElement.GetProperty("type").GetString(), absentProblem.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            TryGetProblemDetail(otherOwnerProblem.RootElement),
            TryGetProblemDetail(absentProblem.RootElement));
    }

    [Fact]
    public async Task Invalid_identity_and_invalid_json_are_rejected_without_owner_fallback()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authorize(client, factory.CreateToken(subject: null, extraClaims: [new Claim(ClaimTypes.NameIdentifier, "fallback")])) ;
        var missingSubject = await client.GetAsync("/api/profile");

        Authorize(client, factory.CreateDuplicateSubjectToken());
        var duplicateSubject = await client.GetAsync("/api/profile");

        using var validClient = factory.CreateClient();
        Authorize(validClient, factory.CreateToken("json"));
        var unknownProperty = await SendJsonAsync(validClient, HttpMethod.Post, "/api/profile", """{"headline":"Engineer","evidence":[],"preferences":{"preferredArrangement":"remote","remoteOnly":true},"owner":"forbidden"}""");
        var duplicateProperty = await SendJsonAsync(validClient, HttpMethod.Post, "/api/profile", """{"headline":"Engineer","headline":"Architect","evidence":[],"preferences":{"preferredArrangement":"remote","remoteOnly":true}}""");
        var invalidPrecision = await SendJsonAsync(validClient, HttpMethod.Post, "/api/profile", ProfileJson("Engineer", null).Replace("80.01", "80.001", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.Forbidden, missingSubject.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, duplicateSubject.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownProperty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateProperty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPrecision.StatusCode);
    }

    [Fact]
    public async Task Duplicate_create_stale_revision_and_non_development_matching_are_rejected()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authorize(client, factory.CreateToken("conflict"));
        Assert.Equal(HttpStatusCode.Created, (await SendJsonAsync(client, HttpMethod.Post, "/api/profile", ProfileJson("Engineer", null))).StatusCode);

        var duplicate = await SendJsonAsync(client, HttpMethod.Post, "/api/profile", ProfileJson("Engineer", null));
        var stale = await SendJsonAsync(client, HttpMethod.Put, "/api/profile", ProfileJson("Architect", 2));
        var matching = await client.PostAsync("/api/matching/evaluate", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, matching.StatusCode);
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string? TryGetProblemDetail(JsonElement problem) =>
        problem.TryGetProperty("detail", out var detail) ? detail.GetString() : null;

    private static Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method, string path, string json) =>
        client.SendAsync(new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    private static string ProfileJson(string headline, int? expectedVersion) =>
        $$"""{"headline":"{{headline}}","evidence":[{"claim":"synthetic","sourceReference":"fixture","origin":"userProvided"}],"preferences":{"preferredArrangement":"remote","remoteOnly":true,"minimumHourlyRate":80.01,"currency":"EUR","maximumOfficeDaysPerWeek":0}{{(expectedVersion.HasValue ? $",\"expectedVersion\":{expectedVersion}" : string.Empty)}}}""";
}

public sealed class ApiProfileFactory : WebApplicationFactory<global::Program>
{
    private readonly string _connectionString;
    public const string Issuer = "https://issuer.test/";
    public const string Audience = "https://api.coa.test";
    public static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048));
    public static readonly RsaSecurityKey OtherSigningKey = new(RSA.Create(2048));

    public ApiProfileFactory(string connectionString)
    {
        _connectionString = connectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__COA", connectionString);
    }

    public string CreateToken(string? subject, SecurityKey? signingKey = null, IEnumerable<Claim>? extraClaims = null)
    {
        var claims = new List<Claim>();
        if (subject is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
        if (extraClaims is not null)
            claims.AddRange(extraClaims);
        return new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = Issuer,
            Audience = Audience,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256)
        });
    }

    public string CreateDuplicateSubjectToken()
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode($$"""{"iss":"{{Issuer}}","sub":"first","sub":"second","aud":"{{Audience}}","nbf":{{now.AddMinutes(-1).ToUnixTimeSeconds()}},"exp":{{now.AddMinutes(5).ToUnixTimeSeconds()}}}""");
        var unsignedToken = $"{header}.{payload}";
        var signature = SigningKey.Rsa.SignData(
            Encoding.ASCII.GetBytes(unsignedToken),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{unsignedToken}.{Base64UrlEncoder.Encode(signature)}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = Issuer,
            ["Authentication:JwtBearer:Audience"] = Audience,
            ["ConnectionStrings:COA"] = _connectionString
        }));
        builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.ConfigurationManager = null;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = SigningKey,
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextFactory<COA.Infrastructure.ProfilePersistence.CoaDbContext>>();
            services.RemoveAll<DbContextOptions<COA.Infrastructure.ProfilePersistence.CoaDbContext>>();
            services.AddDbContextFactory<COA.Infrastructure.ProfilePersistence.CoaDbContext>(options =>
                options.UseSqlServer(_connectionString));
        });
    }
}
