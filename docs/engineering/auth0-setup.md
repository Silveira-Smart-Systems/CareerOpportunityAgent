# Auth0 setup for COA — proposed implementation checklist

Tracking: #20; ADR-0010. Do not store real tenant details or tokens in this repository.

## Auth0 dashboard (human setup)
1. Create a dedicated development Auth0 tenant.
2. Register an application of type **Single Page Application** for React.
3. Register an API resource with a stable unique identifier (example `https://api.coa.example`; placeholder, not a live URL).
4. Configure development callback URLs, logout URLs, and allowed web origins with **exact** frontend origins. Never use wildcard production origins.
5. Keep public self-signup disabled for User Zero until explicitly approved.
6. Verify API access token issuance with the intended audience and RS256 signature.

## React SPA
- Use the supported Auth0 React/OIDC SDK with Authorization Code + PKCE.
- Request `openid profile email` and the registered API audience; only add scopes actually needed.
- Keep tokens in memory; do not persist tokens to localStorage.
- Handle login/logout, token renewal, expired sessions, and API 401/403.
- Use HTTPS in deployed environments and a restrictive Content Security Policy.
- Consider BFF with server-side session before beta.

## ASP.NET Core API
- Add `Microsoft.AspNetCore.Authentication.JwtBearer` (version aligned to .NET 10).
- Configure `Authority` to Auth0 issuer and `Audience` to the registered API identifier.
- Validate signature, issuer, audience, lifetime; require HTTPS metadata outside explicit local development.
- Use `MapGroup("/api/profile").RequireAuthorization()` when authenticated endpoints are implemented.
- Derive `OwnerSubject` from validated `iss` and `sub` claims; never trust request-body ownership.
- Keep `/health` anonymous; assess whether synthetic `/api/matching/evaluate` should be secured or removed before deployment.
- Do not enable a global bypass or mock authentication outside tests.

## Configuration placeholders
```json
{
  "Authentication": {
    "Authority": "https://YOUR_TENANT_REGION.auth0.com/",
    "Audience": "https://api.coa.example"
  }
}
```

No client secret belongs in the SPA. Tenant domain, client ID and API audience are public configuration, but secrets and access tokens must remain protected.

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
5. Integrate React login and assess BFF/security before beta.

## References
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
- https://auth0.github.io/aspnetcore-api/articles/configuration.html
