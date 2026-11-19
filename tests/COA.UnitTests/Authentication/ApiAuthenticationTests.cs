using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using COA.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace COA.UnitTests.Authentication;

public sealed class ApiAuthenticationTests : IClassFixture<ApiAuthenticationFactory>
{
    private readonly ApiAuthenticationFactory _factory;

    public ApiAuthenticationTests(ApiAuthenticationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_is_anonymous()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_missing_token()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Jwt_configuration_is_required()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment();

        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddApiJwtBearerAuthentication(configuration, environment);
        });
    }

    [Fact]
    public async Task Me_returns_only_validated_issuer_and_subject()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.CreateToken("user-123"));

        var response = await client.GetAsync("/api/me");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var properties = document.RootElement.EnumerateObject().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, properties.Length);
        Assert.Equal(ApiAuthenticationFactory.Issuer, document.RootElement.GetProperty("issuer").GetString());
        Assert.Equal("user-123", document.RootElement.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Me_rejects_an_invalid_signature()
    {
        var token = _factory.CreateToken("user-123", signingKey: ApiAuthenticationFactory.OtherSigningKey);

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_wrong_issuer()
    {
        var token = _factory.CreateToken("user-123", issuer: "https://other-issuer.test/");

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_wrong_audience()
    {
        var token = _factory.CreateToken("user-123", audience: "coa-web-client-id");

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_an_expired_token()
    {
        var token = _factory.CreateToken(
            "user-123",
            expires: DateTime.UtcNow.AddMinutes(-2),
            notBefore: DateTime.UtcNow.AddMinutes(-10));

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_forbids_a_token_without_a_subject()
    {
        var token = _factory.CreateToken(subject: null);

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> GetMeAsync(string token)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/me");
    }
}

public sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "COA.UnitTests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public sealed class ApiAuthenticationFactory : WebApplicationFactory<global::Program>
{
    public const string Issuer = "https://issuer.test/";
    public const string Audience = "https://api.coa.test";
    public static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048));
    public static readonly RsaSecurityKey OtherSigningKey = new(RSA.Create(2048));

    public string CreateToken(
        string? subject,
        string? issuer = null,
        string? audience = null,
        SecurityKey? signingKey = null,
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        IEnumerable<Claim> claims = subject is null
            ? Array.Empty<Claim>()
            : [new Claim(JwtRegisteredClaimNames.Sub, subject)];
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Audience,
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256)
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:COA", "Server=unused;Database=COA_Authentication_Test");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = Issuer,
            ["Authentication:JwtBearer:Audience"] = Audience,
            ["ConnectionStrings:COA"] = "Server=unused;Database=COA_Authentication_Test"
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
    }
}
