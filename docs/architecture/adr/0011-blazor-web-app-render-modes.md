# ADR-0011: Blazor Web App as COA frontend

- **Status:** Proposed — product owner selected Blazor; technical review pending
- **Date:** 2026-11-17
- **Related:** #20, #23, ADR-0010, ADR-0002

## Context
The MVP originally proposed React. No React frontend has been implemented. The product owner prefers Blazor to reduce frontend complexity and take advantage of server-first interactive rendering.

## Decision
Adopt **Blazor Web App on .NET 10**, using Static SSR for public pages and **Interactive Server** for authenticated MVP screens. Configure render modes per page/component, not globally by default. Interactive Auto may be used later for suitable components, but requires WebAssembly-compatible dependencies and API boundaries.

Use **Auth0 OIDC** through a confidential **Regular Web Application** registration, server-side OpenID Connect middleware and an HttpOnly Secure cookie session. Authorization Code flow with PKCE; server holds tokens if required. Do not use SPA Auth0 SDK or browser localStorage for tokens.

Keep `COA.Api` for external/integration access. Blazor server-side components call Application use cases directly where appropriate; do not introduce redundant HTTP calls to COA.Api for every UI interaction. The API uses JWT bearer tokens independently when accessed by external clients. Auth0 API resource identifier is an audience, not the Blazor callback URL.

## Trade-offs
Interactive Server uses SignalR circuits and server resources; evaluate capacity, reconnect behavior, data protection key persistence, antiforgery protection and distributed deployment before scaling. Interactive Auto introduces dual-runtime constraints and is not the MVP default.

## Migration impact
- Supersedes React frontend selection in ADR-0002 and relevant architecture/product documentation; update these documents in subsequent edits before considering migration complete.
- ADR-0010's React SPA authentication flow is superseded for COA Web; retain API JWT bearer validation for future API clients.
- No frontend or authentication code is implemented by this ADR.

## Implementation order
1. Bootstrap `src/COA.Web` as .NET 10 Blazor Web App with Interactive Server and HTTPS development launch URL `https://localhost:7043`.
2. Configure Auth0 server-side OIDC with cookie session, `/signin-oidc` callback and logout flow. Read settings from environment/User Secrets; no credentials in Git.
3. Add protected `/dashboard` and login/logout routes, and test unauthorized access and redirects.
4. Add profile UI only after authenticated owner-scoped persistence is implemented.
5. Evaluate Interactive Auto for isolated components based on measured benefit.

## Security
Never commit client secret, tokens, or real personal data. Use HTTPS, exact redirect origins, server-side antiforgery and explicit authorization. Secure cookies are not a substitute for owner-scoped Application authorization.
