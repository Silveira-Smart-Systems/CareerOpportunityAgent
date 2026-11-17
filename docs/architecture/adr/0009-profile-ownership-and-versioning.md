# ADR-0009: Authenticated ownership and immutable profile versions

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #20
- **Related:** #14, #1

## Context
The COA profile contains personal career data. The initial User Zero scope does not eliminate authentication and access-control risks. Profile changes must preserve historical evidence and support concurrency safety.

## Decision
Require a validated authenticated identity for profile access; derive ownership from the server-side identity subject. Persist profile revisions as immutable snapshots with an optimistic expected-version check. Application owns use cases and ports; Infrastructure owns SQL Server persistence; Domain remains provider-agnostic.

An OIDC provider versus local identity implementation choice remains **open** and requires an explicit follow-up decision before exposing personal-data endpoints.

## Alternatives
- Unauthenticated single-user CRUD: rejected due to privacy risk.
- Mutable single-row profile: rejected because evidence/history could be lost.
- Full event sourcing: rejected as unnecessary complexity for MVP.
- Eager multi-tenant identity architecture: deferred until commercial validation.

## Consequences
Requires identity configuration, migration schema, authorization/integration tests, and privacy controls before profile APIs are usable. Historical snapshots increase storage and deletion-policy complexity.

## Revisit when
Multi-user validation, identity-provider constraints, or retention obligations require revised ownership or history strategy.

## Approval
Proposed only; human review required before Accepted.
