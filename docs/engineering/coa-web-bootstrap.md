# COA.Web — Auth0 OIDC bootstrap

Status: Draft, unverified build and login.

Requires .NET 10 SDK and Auth0 Regular Web Application with callback `https://localhost:7043/signin-oidc`.

```bash
dotnet user-secrets init --project src/COA.Web
dotnet user-secrets set "Authentication:Auth0:Domain" "YOUR_TENANT.eu.auth0.com" --project src/COA.Web
dotnet user-secrets set "Authentication:Auth0:ClientId" "YOUR_CLIENT_ID" --project src/COA.Web
dotnet user-secrets set "Authentication:Auth0:ClientSecret" "YOUR_CLIENT_SECRET" --project src/COA.Web
dotnet dev-certs https --trust
dotnet run --project src/COA.Web --launch-profile https
```

Navigate to `https://localhost:7043`. Dashboard is protected by `[Authorize]`.

**Limitations:** local cookie logout does not terminate Auth0 SSO session; provider logout is deferred. No CI or real Auth0 login verified. No personal profile endpoints or storage. API JWT bearer auth remains #23. No secrets in GitHub.
