# COA Blazor + Auth0 development setup

Status: Design/implementation checklist. The Auth0 tenant, COA Web application and COA API resource were created and callback/logout/origin values confirmed by the product owner; no credentials are stored in GitHub.

## Auth0 configuration
- COA Web type: **Regular Web Application** (confidential OIDC client).
- Callback: `https://localhost:7043/signin-oidc`
- Logout: `https://localhost:7043/`
- Web origin: `https://localhost:7043`
- API identifier: `https://api.coa.dev` (logical audience, not a live URL).
- Use tenant issuer URL from Auth0 dashboard, including HTTPS and trailing slash.

## Blazor server configuration
- Use `Microsoft.AspNetCore.Authentication.OpenIdConnect` and cookie authentication.
- Configure `Authority`, `ClientId`, `ClientSecret` (server only), `ResponseType = code`, `UsePkce = true`, `CallbackPath = /signin-oidc`.
- Configure OIDC signed-out callback / post-logout redirect explicitly; verify the exact logout flow with Auth0 before enabling it.
- Protect `/dashboard` using authorization and ensure server-side components cannot expose private state before authorization completes.
- Use secure cookie, HTTPS, antiforgery, data-protection key management and safe reverse-proxy forwarded headers.

## Local secrets (example keys; values are placeholders)
```bash
dotnet user-secrets init --project src/COA.Web
dotnet user-secrets set "Authentication:Auth0:Domain" "YOUR_TENANT.eu.auth0.com" --project src/COA.Web
dotnet user-secrets set "Authentication:Auth0:ClientId" "YOUR_CLIENT_ID" --project src/COA.Web
dotnet user-secrets set "Authentication:Auth0:ClientSecret" "YOUR_CLIENT_SECRET" --project src/COA.Web
```

The client secret must never be placed in `appsettings.json`, browser code, GitHub issues or chat messages. Client ID and tenant domain are not secret, but should still be environment-specific.

## Validation
- Anonymous visitor to protected dashboard is challenged and redirected to Auth0.
- Valid login establishes a secure server-side cookie session.
- Logout terminates local session and handles Auth0 logout correctly.
- Invalid OIDC state, nonce, callback, issuer and expired sessions are rejected.
- No private profile API before ownership authorization tests.
- CI build and tests must pass before human-reviewed merge.
