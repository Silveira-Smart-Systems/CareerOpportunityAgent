# Backend state audit — 2026-11-22

Parent: [#89](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/89)  
Status: **Repository inspection only; no code change or implementation approval**

## Verified from main
- `COA.slnx` and .NET 10 CI are present.
- Production source projects: `COA.Api`, `COA.Application`, `COA.Domain`, `COA.Infrastructure`, `COA.Web`.
- Unit tests and SQL Server integration test projects are present.
- API config requires `ConnectionStrings:COA` and uses SQL Server EF Core; JWT bearer auth and owner-scoped profile/search-profile/opportunity endpoints exist.
- Discovery implementation includes legacy and scoped planners/orchestrators, scope validation, execution gate, in-memory reservation authority and a Jobicy experimental adapter.
- `Program.cs` describes Jobicy as opt-in, default disabled, with **no execution wired in the API**.
- Domain/Application matching code and tests exist; persistence migrations for profiles, opportunities and search profiles exist.
- `.github/workflows/ci.yml` has repository hygiene, .NET 10 restore/build/unit tests and SQL Server integration tests.

## Evidence and limits
- Source tree and selected code inspected on `main`; this is **not** a full security/code review.
- Latest observed CI run #149 succeeded for **UX documentation PR #78**, not a fresh backend change; CI success does not prove readiness for provider activation.
- Did not run a local build or tests in this audit.
- Issue #67 explicitly says Phase 4C.2 evolution implementation is blocked pending separate approval; #74 remains design-first.
- Issue #46 governs source rights; #47 (catalog/ingestion/dedup) is outside the currently authorized discovery increment.

## Recommended backend critical path
1. Inspect and reconcile the actual scope of **already merged** discovery contracts, gates and tests against #56/#67/#72/#74, and identify gaps without assuming planned work is absent.
2. Choose a **small, fully synthetic, provider-neutral vertical verification slice**: scoped request -> planner -> gate -> fake adapter -> outcome/diagnostics; verify mandatory fail-closed, cancellation, concurrency and provenance Unknown.
3. Define exact test names, negative cases and acceptance criteria; avoid new runtime code until explicit implementation gate.
4. Separate future real-provider rights, distributed gate, persistence/dedup and end-to-end product pipeline into distinct approvals.
5. UX Concept A remains a non-blocking exploratory workstream; do not expand backend scope for affiliate courses #87.

## Approval gates
- **Allowed now:** inspect code/CI, plan, document, comment on issues, propose scoped PR.
- **Not authorized by #89:** implement runtime code, merge, deploy, enable Jobicy, use real provider HTTP, add credentials, launch scheduler, or incur costs.

## Immediate next decision
Present a narrowly scoped implementation proposal for backend tests/verification of the existing synthetic discovery execution path. Obtain the explicit implementation approval required by #67 before any code changes.
