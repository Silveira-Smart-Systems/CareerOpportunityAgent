using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace COA.Api.Authentication;

public sealed record AuthenticatedOwner(string Issuer, string Subject)
{
    public static bool TryCreate(
        ClaimsPrincipal principal,
        [NotNullWhen(true)] out AuthenticatedOwner? owner)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            owner = null;
            return false;
        }

        var issuer = principal.FindFirst(JwtRegisteredClaimNames.Iss)?.Value;
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
        {
            owner = null;
            return false;
        }

        owner = new AuthenticatedOwner(issuer, subject);
        return true;
    }
}

public sealed record CurrentIdentityResponse(string Issuer, string Subject);
