# Auth0 setup for COA — proposed implementation checklist

Tracking: #20, #23; ADR-0010, ADR-0011. Do not store real tenant details or tokens in this repository.

## Auth0 dashboard (human setup)
1. Create a dedicated development Auth0 tenant.
2. Register COA Web as a **Regular Web Application** for the confidential server-side OIDC flow in ADR-0011.
3. Register an API resource with a stable unique identifier (example `https://api.coa.example`; placeholder, not a live URL).
4. Configure development callback URLs, logout URLs, and allowed web origins with **exact** frontend origins. Never use wildcard production origins.
5. Keep public self-signup disabled for User Zero until explicitly approved.
6. Verify API access token issuance with the intended audience and RS256 signature.

## COA Web
- COA Web uses server-side OIDC with Authorization Code + PKCE and an HttpOnly cookie session; see ADR-0011 and `blazor-auth0-local-setup.md`.
- Do not send browser tokens to COA.Api for Blazor Server screens that call Application use cases directly.
- Never use an ID token as a COA.Api bearer token.

## ASP.NET Core API
- Add `Microsoft.AspNetCore.Authentication.JwtBearer` (version aligned to .NET 10).
- Configure `Authority` to Auth0 issuer and `Audience` to the registered API identifier.
- Validate signature, issuer, audience, lifetime; require HTTPS metadata outside explicit local development.
- Use `MapGroup("/api/profile").RequireAuthorization()` when authenticated endpoints are implemented.
- Derive `OwnerSubject` from validated `iss` and `sub` claims; never trust request-body ownership.
- Keep `/health` anonymous. The synthetic `/api/matching/evaluate` endpoint remains anonymous in this increment because it has no persistence or private inputs and is not an authorization authority; secure or remove it before exposing it beyond controlled development use.
- Do not enable a global bypass or mock authentication outside tests.

## Configuration placeholders
```json
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://YOUR_TENANT_REGION.auth0.com/",
      "Audience": "https://api.coa.example"
    }
  }
}
```

No client secret belongs in browser code. Tenant domain, client ID and API audience are public configuration, but secrets and access tokens must remain protected.

## Tests / approval gates
- Missing token => 401; wrong issuer/audience/signature/expired token => 401.
- Valid token can access only its own profile; other owner => 404.
- Concurrency/version conflict => 409.
- No unauthenticated read/write; no owner ID from client overrides server identity.
- No real CVs in fixtures; record CI evidence in PR.
- Human review before merge and before enabling real profile data.

## Implementation sequence
1. Review ADR-0010 and select development tenant.
2. Implement API authentication middleware and minimal protected `/api/me` endpoint using synthetic data.
3. Implement owner-scoped Application port and SQL Server immutable version persistence.
4. Add authenticated profile CRUD and authorization/integration tests.
5. Reassess whether API clients are needed before exposing additional bearer-protected endpoints.

## References
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
- https://auth0.github.io/aspnetcore-api/articles/configuration.html
