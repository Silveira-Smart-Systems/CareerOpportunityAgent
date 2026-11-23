# COA-004 F1 authenticated acceptance runbook

Status: preparation only. It does not provide authenticated acceptance evidence.  
Related: #49, Draft PR #97, P0 contract `docs/project/coa-004-p0-execution-contracts.md`.

## Scope and safety boundary

Use this runbook only with an approved non-production Auth0/API environment and synthetic or irreversibly redacted data. Do not print, commit, paste, or inspect secret values, bearer tokens, user-secrets contents, or real opportunity/profile data. This runbook does not authorize creating Auth0 resources, accounts, credentials, databases, provider connections, deployments, or spending.

The Web application is an Interactive Server application with server-side OIDC and an HTTP API client. A local browser check is not equivalent to authenticated acceptance unless both the configured Web OIDC flow and API bearer validation are working.

## Presence-only preflight

Do not run `dotnet user-secrets list`, echo environment variables, or open configuration files that contain values. The following PowerShell check reports only whether expected configuration locations exist:

```powershell
$checks = [ordered]@{
  WebApiBaseUrl = Test-Path Env:COA_API_BASE_URL
  WebAuth0Domain = Test-Path Env:Authentication__Auth0__Domain
  WebAuth0ClientId = Test-Path Env:Authentication__Auth0__ClientId
  WebAuth0ClientSecret = Test-Path Env:Authentication__Auth0__ClientSecret
  WebAuth0ApiAudience = Test-Path Env:Authentication__Auth0__ApiAudience
  ApiConnectionString = Test-Path Env:ConnectionStrings__COA
  ApiJwtAuthority = Test-Path Env:Authentication__JwtBearer__Authority
  ApiJwtAudience = Test-Path Env:Authentication__JwtBearer__Audience
}
$checks.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }
```

Equivalent hierarchical configuration keys may be provided by an approved secret store rather than environment variables; obtain a human confirmation of their presence without reading values. The Web also accepts `CoaApi:BaseUrl` in its secure configuration source. Its required Auth0 keys are `Authentication:Auth0:Domain`, `ClientId`, `ClientSecret`, and `ApiAudience`; `ApiScope` is optional. The API requires `ConnectionStrings:COA`, `Authentication:JwtBearer:Authority`, and `Authentication:JwtBearer:Audience`.

Observed in this worktree on 2026-11-23: the environment-variable presence checks above were all `False` and no local development settings file exists. No values or user secrets were read.

## Approved-environment setup

1. Confirm a non-production Auth0 Regular Web Application has exact values for `https://localhost:7043/signin-oidc`, `https://localhost:7043/` logout, and `https://localhost:7043` origin. The API audience is a logical identifier, not a URL to browse.
2. Confirm an isolated non-production SQL database is available. The API must receive its secure connection string and JWT authority/audience. Apply migrations only under the database owner's existing approval:

   ```powershell
   dotnet ef database update --project src/COA.Infrastructure --startup-project src/COA.Api
   ```

3. Start the API before the Web application. The default development launch URLs are `https://localhost:52363` (API) and `https://localhost:7043` (Web):

   ```powershell
   dotnet run --project src/COA.Api
   dotnet run --project src/COA.Web --launch-profile https
   ```

4. Use only a test owner and synthetic opportunity data. In Development, `Demo__EnableSyntheticSeed=true` enables the owner-scoped, idempotent `/api/opportunities/seed-synthetic` endpoint after normal authenticated authorization. Do not place a bearer token in shell history, screenshots, logs, or PR comments.
5. Capture sanitized screenshots only after an observed test. Do not record browser cookies, callback query strings, tokens, identities, original job URLs, or personal data.

## Browser tooling and UI-only alternatives

This repository currently has no browser/E2E project, Playwright configuration, or component-test host with an authentication override. A fake API or fake authentication scheme would require new test-host infrastructure and would not validate real Auth0 OIDC, API bearer validation, server cookie handling, or the protected Interactive Server circuit. It is therefore outside this F1 preparation slice and must be labelled **UI-only simulated testing**, not authenticated acceptance, if separately authorized later.

## Authenticated acceptance matrix

Run each row at a 1440px desktop viewport and a 390px narrow viewport where applicable. Mark **Observed** only with actual results; retain **Not executed** when the approved environment is unavailable.

| Check | Expected observation | Desktop | Narrow | Status on 2026-11-23 |
|---|---|---|---|---|
| Sign-in and dashboard | `/dashboard` redirects unauthenticated users to Auth0; a valid test session reaches the dashboard without private pre-auth content. | Screenshot + result | Screenshot + result | Not executed — approved environment absent |
| List loading and empty state | Loading has a status announcement; empty list explains that no opportunities are imported. | Screenshot + result | Screenshot + result | Not executed |
| Synthetic list/detail | Synthetic records retain the **Synthetic demo** label; external link names the new-tab behavior. | Screenshot + result | Screenshot + result | Not executed |
| Assessment semantics | Score and eligibility are separate; 0% can remain eligible when no required criterion is unmet; matched, missing, and unknown/not-proven have distinct labels. | Screenshot + result | Screenshot + result | Not executed |
| Keyboard and focus | Tab order reaches dashboard/list/detail actions, form controls, refresh/retry and links; visible focus remains present. | Steps + result | Steps + result | Not executed |
| Screen reader semantics | Headings, status/alert announcements, form labels, details summary, synthetic marker, and external-link disclosure are announced meaningfully. | Tool + result | Tool + result | Not executed |
| 401 | Expired/missing API access produces **Sign in required** and a sign-in action, not Retry. | Screenshot + result | Screenshot + result | Not executed |
| 403 | Access denial produces **Access denied**, no retry, and no opportunity title/description/evidence leak. | Screenshot + result | Screenshot + result | Not executed |
| 404 | Detail produces **Opportunity not found** with no retry and no detail leak. | Screenshot + result | Screenshot + result | Not executed |
| 500/transient | Failure exposes no response body; Retry is available and re-attempts the request. | Screenshot + result | Screenshot + result | Not executed |

## Evidence handoff

The reviewer should attach only sanitized screenshots and a completed matrix to the Draft PR, state the exact environment class (non-production), browser and viewport, and request a fresh independent Frontend UX/Test review. If the presence-only preflight fails, keep the PR Draft and request the smallest human action: access to an approved non-production preconfigured environment or a pre-authenticated test session.
