using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using COA.Application.Profile;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JwtClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

namespace COA.Api.Authentication;

public sealed record AuthenticatedOwner(string Issuer, string Subject)
{
    private const string InvalidIdentityShapeClaim = "coa:validated-jwt-identity-shape-invalid";

    public override string ToString() => "AuthenticatedOwner { Redacted }";

    public static bool TryCreate(
        ClaimsPrincipal principal,
        [NotNullWhen(true)] out AuthenticatedOwner? owner)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            owner = null;
            return false;
        }

        if (principal.HasClaim(InvalidIdentityShapeClaim, "true"))
        {
            owner = null;
            return false;
        }

        var issuers = principal.FindAll(JwtClaimNames.Iss).Select(claim => claim.Value).ToArray();
        var subjects = principal.FindAll(JwtClaimNames.Sub).Select(claim => claim.Value).ToArray();

        if (issuers.Length != 1 || subjects.Length != 1)
        {
            owner = null;
            return false;
        }

        try
        {
            _ = new OwnerIdentity(issuers[0], subjects[0]);
            owner = new AuthenticatedOwner(issuers[0], subjects[0]);
            return true;
        }
        catch (ArgumentException)
        {
            owner = null;
            return false;
        }
    }

    public static bool TryCreateOwnerIdentity(
        ClaimsPrincipal principal,
        [NotNullWhen(true)] out OwnerIdentity? owner)
    {
        if (!TryCreate(principal, out var authenticatedOwner))
        {
            owner = null;
            return false;
        }

        owner = new OwnerIdentity(authenticatedOwner.Issuer, authenticatedOwner.Subject);
        return true;
    }

    internal static void MarkInvalidIdentityShape(ClaimsPrincipal? principal, SecurityToken? securityToken)
    {
        if (principal is null || HasExactlyOneStringIssuerAndSubject(securityToken))
            return;

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(InvalidIdentityShapeClaim, "true"));
        principal.AddIdentity(identity);
    }

    private static bool HasExactlyOneStringIssuerAndSubject(SecurityToken? securityToken)
    {
        var encodedToken = securityToken switch
        {
            JsonWebToken token => token.EncodedToken,
            JwtSecurityToken token => token.RawData,
            _ => null
        };
        if (string.IsNullOrEmpty(encodedToken))
            return false;

        var parts = encodedToken.Split('.');
        if (parts.Length != 3)
            return false;

        try
        {
            var reader = new Utf8JsonReader(Base64UrlEncoder.DecodeBytes(parts[1]));
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return false;

            var issuerCount = 0;
            var subjectCount = 0;
            var completedRootObject = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    completedRootObject = true;
                    break;
                }
                if (reader.TokenType != JsonTokenType.PropertyName)
                    return false;

                var propertyName = reader.GetString();
                if (!reader.Read())
                    return false;

                if (propertyName == JwtClaimNames.Iss)
                {
                    issuerCount++;
                    if (reader.TokenType != JsonTokenType.String)
                        return false;
                }
                else if (propertyName == JwtClaimNames.Sub)
                {
                    subjectCount++;
                    if (reader.TokenType != JsonTokenType.String)
                        return false;
                }
                else
                {
                    reader.Skip();
                }
            }

            return completedRootObject && !reader.Read() && issuerCount == 1 && subjectCount == 1;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }
}

public sealed record CurrentIdentityResponse(string Issuer, string Subject);
