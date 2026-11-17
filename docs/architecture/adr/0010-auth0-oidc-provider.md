# ADR-0010: Auth0 as external OIDC identity provider

- **Status:** Proposed — provider selected by product owner; technical review pending
- **Date:** 2026-11-17
- **Issue:** #20
- **Related:** ADR-0009, #14

## Context
COA stores private career data. The MVP needs secure authentication without maintaining passwords, registration and password recovery infrastructure. React SPA and ASP.NET Core 10 API are the current stack.

## Decision
Use **Auth0** as the initial OIDC provider. Register a **Single Page Application** and a distinct **API resource**. React signs users in using Authorization Code + PKCE and requests an access token with the API's configured audience. The ASP.NET Core API uses standard `Microsoft.AspNetCore.Authentication.JwtBearer` validation with strict issuer, audience, signature, expiry, and HTTPS metadata checks. Never use an ID token as an API bearer token.

For the MVP, keep access tokens in memory rather than browser localStorage. Use short token lifetimes, strict CORS allowlists and HTTPS. Evaluate a confidential Backend-for-Frontend (BFF) with HttpOnly, Secure, SameSite cookies before public beta, especially if browser token exposure or XSS risk requires it.

Identify profile owners by the validated tuple `(iss, sub)` rather than email or a client-supplied user ID. Application authorization checks ownership on every profile operation. Reject unauthenticated access and avoid leaking whether another user's profile exists.

Auth0 configuration belongs in environment configuration. Domain and Application do not depend on Auth0 SDKs. Only the API authentication adapter is provider-specific.

## Alternatives
- Local ASP.NET Core Identity: adds password/account lifecycle maintenance.
- Microsoft Entra External ID: credible alternative, but increases early setup and ecosystem-specific decisions for this MVP.
- No authentication for User Zero: rejected due to private profile data.
- BFF immediately: more secure browser token boundary but introduces session/CSRF/cookie and deployment complexity; reconsider before public beta.

## Consequences
Requires Auth0 tenant, SPA registration, API audience, callback/logout/origin allowlists, token validation and integration tests. The free plan is an initial commercial hypothesis, not a guaranteed entitlement. Provider-specific claims/quotas must not leak into Domain.

## Security requirements
- Never commit tenant credentials or real tokens.
- Disable public self-registration until deliberately enabled.
- API access tokens must have correct `aud`; an ID token is not acceptable.
- Validate signature, issuer, audience, expiration; deny by default.
- No private profile endpoint before authenticated owner-scoping tests pass.
- Use synthetic identities and data in CI.
- Confirm data processing, retention, and hosting considerations before multi-user beta.

## Revisit when
Public beta, security review, account linking, data residency, provider cost, or tenant portability requires a BFF or provider change.

## References
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
- https://developer.auth0.com/resources/code-samples/api/aspnet-core/basic-authorization
- https://auth0.com/pricing
