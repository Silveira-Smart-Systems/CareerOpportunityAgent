using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace COA.Api.Authentication;

public static class ApiJwtBearerAuthenticationExtensions
{
    public static IServiceCollection AddApiJwtBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authority = configuration["Authentication:JwtBearer:Authority"];
        var audience = configuration["Authentication:JwtBearer:Audience"];

        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        {
            if (!environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    "Authentication:JwtBearer:Authority and Authentication:JwtBearer:Audience must be configured.");
            }

            authority = "https://test.invalid/";
            audience = "test-audience";
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        AuthenticatedOwner.MarkInvalidIdentityShape(context.Principal, context.SecurityToken);
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await Results.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Authentication required",
                            type: "https://httpstatuses.com/401").ExecuteAsync(context.HttpContext);
                    }
                };
            });

        return services;
    }
}
