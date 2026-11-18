# ADR-0012: EF Core SQL Server profile persistence and owner isolation

- **Status:** Accepted
- **Date:** 2026-11-18
- **Issue:** #32 (parent #20)
- **Related:** ADR-0009, ADR-0010, ADR-0011, #33, #34, #35

## Context

COA has Domain profile invariants and validated API identity from #31, but no persistence or owner-scoped profile API. The MVP requires version history, predictable concurrent edits, and privacy controls without adding a second persistence approach or exposing personal data before integration tests pass.

## Decision

Use **EF Core 10 with SQL Server only** for profile persistence. Add `COA.Infrastructure` in #34 to own the `DbContext`, Fluent API mappings, SQL transactions, and EF migrations. Domain and Application remain free of EF Core and SQL Server dependencies. Do not introduce Dapper or a generic repository.

Use the validated, case-sensitive tuple `(iss, sub)` as an opaque owner identity. Infrastructure derives a fixed-size SHA-256 owner lookup key from the length-prefixed UTF-8 tuple, stores the original values with a binary SQL collation, and verifies them after lookup. This avoids case-insensitive identity aliasing and SQL Server composite-index key-size limits. Email and request-supplied ownership are never used.

For the User Zero MVP, allow exactly one profile per owner. Persist each revision as an append-only snapshot and evidence set. Profile creation is `POST`, not an upsert; revisions use `PUT` with an expected version. A unique `Profiles.OwnerId` constraint makes concurrent creation atomic: one request succeeds and a duplicate-key outcome for the same authenticated owner maps to `409`. Infrastructure performs an atomic owner-scoped compare-and-swap plus snapshot insert in one SQL transaction. `CurrentVersion` is the public concurrency contract; `rowversion` is an EF Core safeguard, not the client contract.

`MinimumHourlyRate` is stored as `decimal(18,2)`. #33 must reject an input that is not exactly representable in increments of 0.01 at the Application/Domain boundary; it must not silently round before persistence.

COA Web uses its server-side OIDC/cookie session to create the Application owner identity and calls Application directly. COA.Api independently validates Auth0 API access tokens and creates the same identity. This preserves ADR-0011's no-redundant-HTTP path for Blazor and ADR-0010's independent bearer API boundary.

Use real isolated SQL Server databases for persistence/integration tests in CI. Apply migrations explicitly; do not apply production migrations automatically on API startup.

## Consequences

- #33 defines Application contracts before Infrastructure implementation.
- #34 introduces EF Core 10 SQL Server packages, mappings, migrations, and transaction/concurrency tests.
- #35 adds owner-scoped API endpoints only after persistence tests pass.
- Reads and writes always scope by owner. A missing or other-owner profile returns `404`; a stale revision of an existing own profile returns `409`.
- Snapshot rows are immutable during normal operation. Approved account deletion is the exception and must remove the profile graph according to the retention policy.
- The proposed initial operational target is for backups that can contain deleted personal data to expire within 30 days, subject to selected infrastructure capability and legal obligations. The policy, retention evidence, and restore procedure must be confirmed before real personal data is persisted; this is a go-live blocker, not an implemented guarantee. A restore must re-apply verified deletion records before restored profile data is made available, preventing deleted data from being reintroduced.

## Alternatives

- **Composite unique index on issuer + subject:** rejected because default SQL collation can be case-insensitive and large claim values can exceed index-key limits.
- **Case-insensitive normalized identity:** rejected because identity claims are opaque and case-sensitive.
- **Dapper or a generic repository:** rejected; EF Core migrations/configurations and focused Application ports are sufficient for this MVP.
- **Mutable profile tables only:** rejected because profile evidence/history would be overwritten.
- **Event sourcing:** rejected as unnecessary complexity for the MVP.
- **Automatic database migration at API startup:** rejected because it couples runtime availability and schema privileges.

## Decisions requiring human approval

1. Approve one owner/one profile for the MVP.
2. Approve SHA-256 owner lookup keys plus stored binary-collated issuer/subject, including the 2,048-character limit and rejection of oversized identity claims.
3. Approve application/EF-enforced immutability initially; database principal permissions that deny snapshot updates/deletes remain deployment defence-in-depth work.
4. Approve hard deletion on a verified request and the proposed backup-expiry target of up to 30 days, subject to infrastructure capability and legal obligations, before real personal data is stored.
5. Approve a CI SQL Server service and explicit migration step; no production auto-migration.

## Supersession on acceptance

When this ADR is Accepted, it supersedes the open provider/persistence implementation choices in ADR-0009. Until then, ADR-0009 remains Proposed and pending supersession.
