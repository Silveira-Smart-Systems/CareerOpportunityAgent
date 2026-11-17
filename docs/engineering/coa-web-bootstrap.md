# COA.Web — Auth0 OIDC bootstrap

Status: Local Auth0 login and anonymous dashboard access tested manually; provider logout pending manual verification.

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

## Logout validation

1. Sign in through Auth0 and confirm `/dashboard` is accessible.
2. Click **Sign out**. The dashboard submits a POST request with an antiforgery token; the server signs out of both the local cookie and the OIDC scheme.
3. Verify the Auth0 logout redirect returns to `https://localhost:7043/`.
4. In a private window, verify `/dashboard` does not reveal protected content.
5. In the original window, start a new sign-in and check whether Auth0 requests credentials again. Auth0 session behavior may also depend on federated identity-provider sessions and tenant configuration.

Auth0 **Allowed Logout URLs** must include `https://localhost:7043/`. The Auth0 tenant's discovery metadata must expose a compatible OIDC end-session endpoint for the OpenIdConnect handler to perform provider logout; verify this in the browser/network trace if the provider session persists.

**Limitations:** Provider logout has not yet been manually verified. No personal profile endpoints or storage. API JWT bearer auth remains #23. Never commit secrets to GitHub.
