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
        var revised = await JsonDocument.ParseAsync(await revise.Content.ReadAsStreamAsync());
        Assert.Equal("Architect", revised.RootElement.GetProperty("headline").GetString());
        Assert.Equal("Engineer", (await JsonDocument.ParseAsync(await historical.Content.ReadAsStreamAsync())).RootElement.GetProperty("headline").GetString());
        Assert.Equal("synthetic", revised.RootElement.GetProperty("evidence")[0].GetProperty("claim").GetString());
        Assert.Equal(80.01m, revised.RootElement.GetProperty("preferences").GetProperty("minimumHourlyRate").GetDecimal());
        Assert.Equal("EUR", revised.RootElement.GetProperty("preferences").GetProperty("currency").GetString());
    }

    [Fact]
    public async Task Remote_only_revision_normalizes_office_days_and_preserves_unrelated_profile_data()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authorize(client, factory.CreateToken("remote-only-transition"));

        var create = await SendJsonAsync(client, HttpMethod.Post, "/api/profile", HybridProfileJson(expectedVersion: null));
        var revise = await SendJsonAsync(client, HttpMethod.Put, "/api/profile", ProfileJson("Hybrid engineer", expectedVersion: 1));
        var historical = await client.GetAsync("/api/profile/versions/1");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revise.StatusCode);
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);

        using var revised = await JsonDocument.ParseAsync(await revise.Content.ReadAsStreamAsync());
        var revisedPreferences = revised.RootElement.GetProperty("preferences");
        Assert.Equal(2, revised.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("synthetic", revised.RootElement.GetProperty("evidence")[0].GetProperty("claim").GetString());
        Assert.True(revisedPreferences.GetProperty("remoteOnly").GetBoolean());
        Assert.Equal("remote", revisedPreferences.GetProperty("preferredArrangement").GetString());
        Assert.Equal(0, revisedPreferences.GetProperty("maximumOfficeDaysPerWeek").GetInt32());
        Assert.Equal(80.01m, revisedPreferences.GetProperty("minimumHourlyRate").GetDecimal());
        Assert.Equal("EUR", revisedPreferences.GetProperty("currency").GetString());

        using var original = await JsonDocument.ParseAsync(await historical.Content.ReadAsStreamAsync());
        Assert.Equal("hybrid", original.RootElement.GetProperty("preferences").GetProperty("preferredArrangement").GetString());
        Assert.Equal(3, original.RootElement.GetProperty("preferences").GetProperty("maximumOfficeDaysPerWeek").GetInt32());
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

    [Fact]
    public async Task Other_owner_cannot_read_or_replace_current_or_historical_profile()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var ownerA = factory.CreateClient();
        Authorize(ownerA, factory.CreateToken("owner-a-write"));
        Assert.Equal(HttpStatusCode.Created, (await SendJsonAsync(ownerA, HttpMethod.Post, "/api/profile", ProfileJson("Private", null))).StatusCode);

        using var ownerB = factory.CreateClient();
        Authorize(ownerB, factory.CreateToken("owner-b-write"));
        var current = await ownerB.GetAsync("/api/profile");
        var historical = await ownerB.GetAsync("/api/profile/versions/1");
        var replace = await SendJsonAsync(ownerB, HttpMethod.Put, "/api/profile", ProfileJson("Attempt", 1));

        Assert.Equal(HttpStatusCode.NotFound, current.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, historical.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, replace.StatusCode);
        Assert.Equal("Private", (await JsonDocument.ParseAsync(await (await ownerA.GetAsync("/api/profile")).Content.ReadAsStreamAsync())).RootElement.GetProperty("headline").GetString());
    }

    [Theory]
    [InlineData("{\"evidence\":[],\"preferences\":{\"preferredArrangement\":\"remote\",\"remoteOnly\":true}}")]
    [InlineData("{\"headline\":\"Engineer\",\"evidence\":[],\"preferences\":{\"preferredArrangement\":99,\"remoteOnly\":true}}")]
    [InlineData("{\"headline\":\"Engineer\",\"evidence\":[],\"preferences\":{\"preferredArrangement\":\"unknown\",\"remoteOnly\":true}}")]
    public async Task Invalid_contract_payloads_return_public_bad_request_problem_details(string payload)
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authorize(client, factory.CreateToken("invalid-contract"));

        var response = await SendJsonAsync(client, HttpMethod.Post, "/api/profile", payload);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.DoesNotContain("token", response.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Concurrent_create_and_revision_leave_one_current_immutable_history()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var token = factory.CreateToken("concurrent");
        Authorize(first, token);
        Authorize(second, token);

        var creates = await Task.WhenAll(
            SendJsonAsync(first, HttpMethod.Post, "/api/profile", ProfileJson("Initial", null)),
            SendJsonAsync(second, HttpMethod.Post, "/api/profile", ProfileJson("Initial", null)));
        Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Conflict);

        var revisions = await Task.WhenAll(
            SendJsonAsync(first, HttpMethod.Put, "/api/profile", ProfileJson("Revision A", 1)),
            SendJsonAsync(second, HttpMethod.Put, "/api/profile", ProfileJson("Revision B", 1)));
        Assert.Single(revisions, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(revisions, response => response.StatusCode == HttpStatusCode.Conflict);

        var current = JsonDocument.Parse(await (await first.GetAsync("/api/profile")).Content.ReadAsStringAsync()).RootElement;
        var snapshot = JsonDocument.Parse(await (await first.GetAsync("/api/profile/versions/1")).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, current.GetProperty("version").GetInt32());
        Assert.Equal("Initial", snapshot.GetProperty("headline").GetString());
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

    private static string HybridProfileJson(int? expectedVersion) =>
        $$"""{"headline":"Hybrid engineer","evidence":[{"claim":"synthetic","sourceReference":"fixture","origin":"userProvided"}],"preferences":{"preferredArrangement":"hybrid","remoteOnly":false,"minimumHourlyRate":80.01,"currency":"EUR","maximumOfficeDaysPerWeek":3}{{(expectedVersion.HasValue ? $",\"expectedVersion\":{expectedVersion}" : string.Empty)}}}""";
}

public sealed class ApiProfileFactory : WebApplicationFactory<global::Program>
{
    private readonly string _connectionString;
    private readonly bool _development;
    private readonly bool _enableSyntheticSeed;
    public const string Issuer = "https://issuer.test/";
    public const string Audience = "https://api.coa.test";
    public static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048));
    public static readonly RsaSecurityKey OtherSigningKey = new(RSA.Create(2048));

    public ApiProfileFactory(string connectionString, bool development = false, bool enableSyntheticSeed = false)
    {
        _connectionString = connectionString;
        _development = development;
        _enableSyntheticSeed = enableSyntheticSeed;
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
        builder.UseEnvironment(_development ? "Development" : "Testing");
        builder.UseSetting("ConnectionStrings:COA", _connectionString);
        builder.UseSetting("Authentication:JwtBearer:Authority", Issuer);
        builder.UseSetting("Authentication:JwtBearer:Audience", Audience);
        builder.UseSetting("Demo:EnableSyntheticSeed", _enableSyntheticSeed.ToString());
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = Issuer,
            ["Authentication:JwtBearer:Audience"] = Audience,
            ["ConnectionStrings:COA"] = _connectionString,
            ["Demo:EnableSyntheticSeed"] = _enableSyntheticSeed.ToString()
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
