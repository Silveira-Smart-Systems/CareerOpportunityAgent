# CareerOpportunityAgent
Single source of truth for Career Opportunity Agent project.

## Local MVP 0.1 runbook

Prerequisites: .NET 10, SQL Server reachable locally, and an Auth0 Regular Web
Application/API resource configured as described in
`docs/engineering/blazor-auth0-local-setup.md`. Use User Secrets or environment
variables only; do not place credentials in source control.

Configure the API connection string and JWT authority/audience, then configure
the Web Auth0 values and `COA_API_BASE_URL`. Apply migrations before starting the
API:

```powershell
dotnet ef database update --project src/COA.Infrastructure --startup-project src/COA.Api
dotnet run --project src/COA.Api
dotnet run --project src/COA.Web
```

For the reproducible demo only, start the API in Development with
`Demo__EnableSyntheticSeed=true`, sign in normally, then POST to
`/api/opportunities/seed-synthetic` with that user's bearer token. The endpoint
is not registered outside Development, requires authorization, seeds only the
current owner and is idempotent. It never contacts a provider. The Web labels
these records **Synthetic demo**.

Manual smoke checklist: sign in; create a career profile and a search profile;
seed demo data; verify another test user cannot list or open it; open a detail;
check score/evidence plus loading, empty and error UI states. Unknown evidence
must be displayed as not proven, not a match.

Validation commands:

```powershell
dotnet restore COA.slnx
dotnet build COA.slnx -c Release
dotnet test COA.slnx -c Release
```

SQL integration tests require an isolated `COA_SQL_TEST_CONNECTION_STRING`; they
create and remove their own database and intentionally do not skip when that
variable is absent.
