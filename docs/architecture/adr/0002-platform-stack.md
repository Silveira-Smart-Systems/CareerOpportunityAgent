# ADR-0002: Initial technology stack

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
The MVP needs fast delivery using a maintainable ecosystem and existing expertise.

## Decision
Use .NET 10 / ASP.NET Core for API and worker, React for UI, SQL Server for persistence, and Docker for local development. Keep Azure hosting as an option, not a mandatory infrastructure commitment.

## Alternatives considered
Node.js or Python backend; Blazor UI; PostgreSQL; early Kubernetes.

## Consequences and trade-offs
Productivity and consistent tooling, with some vendor and SQL Server licensing/hosting considerations. Confirm .NET runtime support and deployment economics at implementation time.

## Revisit when
Operational benchmarks, team requirements, licensing, or cost evidence materially favor another stack.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
