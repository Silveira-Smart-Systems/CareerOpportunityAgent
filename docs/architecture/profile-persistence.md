# Profile persistence and authorization design

Status: Proposed; tracking #20, #14. This document is a design, not an implemented security control.

## Boundary
- Domain defines profile and revision invariants.
- Application defines repository ports and authenticated use cases.
- Infrastructure owns SQL Server mappings, transactions and migrations.
- API validates authenticated identity and calls application use cases. Domain never reads HTTP claims or database contexts.

## Proposed data model
- `Profile`: `Id` (UUID PK), `OwnerSubject` (stable identity subject, indexed), `CurrentVersion`, `CreatedAtUtc`, `UpdatedAtUtc`.
- `ProfileVersion`: composite key `(ProfileId, Version)`, immutable snapshot of headline and preferences, `CreatedAtUtc`, `CreatedBySubject`, optional change reason.
- `ProfileEvidence`: `(ProfileId, Version, Ordinal)`, claim, source reference and evidence origin, FK to profile version.
- Keep historical snapshots immutable; do not overwrite prior version/evidence rows.
- A new revision must atomically verify expected version, insert snapshot/evidence, and update current version. Concurrency mismatch returns a conflict, not last-write-wins.
- Sensitive CV file bytes are out of scope for this increment; document storage, encryption and deletion separately.

## Authentication / authorization decision pending
- User Zero must still authenticate: single-user deployment is not a substitute for access control.
- Evaluate a trusted OIDC provider versus local identity before selecting implementation; use issuer/audience validation and standard bearer-token middleware.
- Owner subject must derive from validated server-side identity, never from caller-provided owner fields.
- All reads and writes are scoped to the authenticated subject. Return not-found for profiles outside scope to avoid enumeration.
- Define a policy for subject migration and account deletion before beta.
- Do not expose profile CRUD routes until auth and ownership integration tests pass.

## API contract proposal
- `GET /api/profile`: current user's profile; 401 unauthenticated, 404 absent.
- `PUT /api/profile`: requires expected version; 409 on stale revision; request cannot specify owner subject.
- `GET /api/profile/versions/{version}`: owner-only historical snapshot.
- Validate lengths, rates, currencies and source references at boundaries and in domain.
- Use structured ProblemDetails responses without private details in logs.

## Operational/privacy controls
- Encrypt transport, use managed SQL credentials/secrets, least privilege, and secure backups.
- Avoid storing full CVs or sensitive prompts in application logs.
- Define retention/deletion of snapshots and evidence before real personal data enters storage.
- Use synthetic fixtures in integration tests and CI.

## Implementation order
1. Agree on authentication provider and record ADR.
2. Introduce Application persistence port and migration schema.
3. Implement SQL Server repository with concurrency/version tests.
4. Add authenticated API endpoints and cross-user authorization tests.
5. Add retention/deletion handling and document operational readiness.

## Non-goals
No public registration, multi-tenant billing, CV AI parsing, automatic applications, or premature event broker.
