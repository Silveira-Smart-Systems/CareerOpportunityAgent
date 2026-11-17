# .NET 10 bootstrap — Issue #12

Status: Draft implementation, awaiting CI and human review.

## Scope
Initial solution contains:
- COA.Domain: deterministic, pure matching and hard-constraint evaluation
- COA.Application: matching use case
- COA.Api: minimal API with health and synthetic evaluation endpoints
- COA.UnitTests: deterministic scoring and constraint tests

This is **not** a complete implementation of FR-01–FR-12. No database, identity, CV parsing, background worker, or AI model integration is present. Those require subsequent focused issues and PRs.

## Local commands
Requires .NET 10 SDK.

```bash
dotnet restore COA.slnx
dotnet build COA.slnx --configuration Release --no-restore
dotnet test COA.slnx --configuration Release --no-build
dotnet run --project src/COA.Api --urls http://localhost:5000
```

## Example synthetic request
```bash
curl -X POST http://localhost:5000/api/matching/evaluate \
  -H 'Content-Type: application/json' \
  -d '{"criteria":[{"code":"technical","evidence":"Matched","weight":40},{"code":"remote","evidence":"Missing","weight":60,"isHardConstraint":true}]}'
```

The expected result is score 40, eligible false. Evidence enums use JSON string conversion.

## Next gates
- CI must actually pass; no local .NET SDK was available to the authoring environment.
- Add API input schema tests and explicit API error contracts.
- Define authentication before accepting private profile data.
- Define persistence/migrations and integration tests before storing opportunities.
- Add architecture tests and agent evaluation fixtures in later vertical slices.
