using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using COA.Web.Api;
using COA.Web.Authentication;
using COA.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITicketStore, InMemoryTicketStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<CoaApiOptions>(options =>
    options.BaseUrl = builder.Configuration["CoaApi:BaseUrl"]
        ?? builder.Configuration["COA_API_BASE_URL"]
        ?? string.Empty);
builder.Services.AddHttpClient<CoaApiClient>((serviceProvider, client) =>
{
    var apiOptions = serviceProvider.GetRequiredService<IOptions<CoaApiOptions>>().Value;
    if (!Uri.TryCreate(apiOptions.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        throw new InvalidOperationException("CoaApi:BaseUrl or COA_API_BASE_URL must be an absolute URI.");
    }

    client.BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + "/");
});

var auth = builder.Configuration.GetSection("Authentication:Auth0");
var domain = auth["Domain"];
var clientId = auth["ClientId"];
var clientSecret = auth["ClientSecret"];
var apiAudience = auth["ApiAudience"];
var apiScope = auth["ApiScope"];

if (string.IsNullOrWhiteSpace(domain) ||
    string.IsNullOrWhiteSpace(clientId) ||
    string.IsNullOrWhiteSpace(clientSecret) ||
    string.IsNullOrWhiteSpace(apiAudience))
{
    throw new InvalidOperationException(
        "Auth0 Domain, ClientId, ClientSecret and ApiAudience must be configured using User Secrets or environment variables.");
}

var authority = domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
    ? domain.TrimEnd('/') + "/"
    : "https://" + domain.TrimEnd('/') + "/";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
})
.AddOpenIdConnect(options =>
{
    options.Authority = authority;
    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.ResponseType = "code";
    options.UsePkce = true;
    options.CallbackPath = "/signin-oidc";
    // SessionStore keeps the token-bearing ticket on the server; the cookie contains only its opaque session key.
    options.SaveTokens = true;
    options.AdditionalAuthorizationParameters["audience"] = apiAudience;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    if (!string.IsNullOrWhiteSpace(apiScope))
    {
        options.Scope.Add(apiScope);
    }
});

builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .PostConfigure<ITicketStore>((options, ticketStore) => options.SessionStore = ticketStore);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = "/dashboard" },
    [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapPost("/logout", () => Results.SignOut(
    new AuthenticationProperties { RedirectUri = "/" },
    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
    .RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
