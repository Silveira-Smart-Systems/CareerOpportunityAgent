# Profile persistence and authorization design

Status: Proposed for human approval in #32. Tracking: #20, #32, #33, #34, #35. This is a design, not an implemented security control.

This document specifies the operational schema and delivery checks. ADR-0012 records the decision and trade-offs; ADR-0010 and ADR-0011 define the identity-provider and Blazor/API boundaries.

## Boundary and identity

- Domain retains `CareerProfile` validation and immutable `Revise`; it never references EF Core, SQL Server, Auth0, claims, or HTTP.
- Application defines an identity value `(Issuer, Subject)`, persistence ports, and create/read/revise use cases. It receives identity only from a trusted adapter.
- Infrastructure contains `COA.Infrastructure`, the EF Core 10 SQL Server `DbContext`, Fluent API configurations, transactions, and migrations. No Dapper or generic repository is used.
- COA Web converts its validated OIDC cookie principal to the Application identity and calls Application directly. COA.Api converts its validated JWT bearer principal to the same identity. Neither caller supplies an owner field.

Issuer and subject are opaque, case-sensitive identifiers. They are not email addresses and are never normalized, case-folded, trimmed, or logged.

An Auth0 tenant, connection, or subject migration must not silently associate a new `(iss, sub)` with an existing profile, including by matching email. The new identity has no profile until a separately approved, audited owner-migration process proves control of both identities; that process is out of scope for this MVP.

## SQL Server schema

| Table | Keys, limits, and constraints | Purpose |
|---|---|---|
| `ProfileOwners` | `OwnerId uniqueidentifier` PK; `OwnerKey binary(32)` unique; `Issuer nvarchar(2048) COLLATE Latin1_General_100_BIN2` not null; `Subject nvarchar(2048) COLLATE Latin1_General_100_BIN2` not null; `CreatedAtUtc datetime2(7)` | One validated external identity. `OwnerKey` is SHA-256 over length-prefixed UTF-8 issuer and subject bytes; lookup verifies the returned binary values as defence against a theoretical hash collision. |
| `Profiles` | `ProfileId uniqueidentifier` PK; `OwnerId` FK and unique; `CurrentVersion int` positive; `RowVersion rowversion`; created/updated `datetime2(7)` | One active profile per owner for the MVP. |
| `ProfileVersions` | PK `(ProfileId, Version)`; FK to `Profiles`; `Headline nvarchar(200)`; preference snapshot fields; `CreatedAtUtc datetime2(7)` | Append-only profile snapshots. |
| `ProfileEvidence` | PK `(ProfileId, Version, Ordinal)`; FK to `ProfileVersions`; `Claim nvarchar(500)`; `SourceReference nvarchar(300)`; `Origin tinyint` | Ordered, provenance-linked evidence for one snapshot. |

`ProfileVersions` stores `PreferredArrangement tinyint`, `RemoteOnly bit`, `MinimumHourlyRate decimal(18,2) null`, `Currency char(3) null`, and `MaximumOfficeDaysPerWeek tinyint null`. Fluent API must configure all limits, FKs, required fields, `CurrentVersion > 0`, the unique owner/profile index, binary collations, and evidence ordinal ordering. The profile limits match current Domain validation; SQL constraints are a second boundary, not a replacement for Domain validation.

The `OwnerKey` avoids SQL Server's composite index key-size limit while retaining case-sensitive identity semantics. Infrastructure computes it only after receiving the validated issuer and subject. Oversized claims above the documented 2,048-character storage limit are rejected as authentication/identity input failures, never truncated.

## Immutable snapshots and concurrency

Creation is distinct from revision:

- `POST /api/profile` creates the single profile for the authenticated owner and returns `201`; a profile that already exists returns `409`. It is not an upsert.
- `PUT /api/profile` revises an existing profile and requires `expectedVersion`; an absent profile returns `404`.

Revision uses one SQL transaction and an atomic compare-and-swap:

1. Update the owner-scoped `Profiles` row only when `CurrentVersion = expectedVersion`.
2. If one row changes, insert version `expectedVersion + 1` and its evidence rows, then commit.
3. If no row changes, query only within the same owner scope: no profile is `404`; an existing profile with another version is `409`.

`CurrentVersion` is the client concurrency contract. `rowversion` is an EF Core concurrency safeguard for tracked root-row writes; it does not replace the atomic SQL compare-and-swap. A failed transaction inserts no partial snapshot. Infrastructure never updates or deletes version/evidence rows during ordinary profile edits. Account deletion is the explicit policy exception.

## Owner-scoped authorization and HTTP semantics

All profile reads and writes include the authenticated owner in the query predicate. There is no profile lookup by ID before the owner scope is applied.

| Operation | Success | Missing/other owner | Other outcome |
|---|---|---|---|
| `GET /api/profile` | `200` current snapshot | `404` | no token `401` |
| `POST /api/profile` | `201` | n/a | already exists `409`; no owner in body |
| `PUT /api/profile` | `200` revised snapshot | `404` | stale expected version `409` |
| `GET /api/profile/versions/{version}` | `200` | `404` | no token `401` |

An authenticated principal without a usable validated issuer/subject is denied; it never falls back to email or a caller value. Returning `404` for another owner's resources prevents owner/profile enumeration. Responses and `ProblemDetails` must not disclose owner identity or raw SQL details.

## Migrations and SQL Server integration tests

- Migrations live in `COA.Infrastructure` and are generated/applied with EF Core tooling through an explicit deployment or CI step. The API must not run production migrations automatically at startup.
- SQL integration tests use an isolated real SQL Server database, apply the migrations, and use synthetic identities/data. EF InMemory is not a substitute.
- CI provisions SQL Server as a service, provides a test-only connection string, and creates a unique database per test run. The fixture migrates before tests and drops that database afterwards.
- Required tests: schema/migration application; create/read snapshot; evidence ordering; version history immutability; concurrent same-version revisions (one success, one conflict); owner A cannot read/revise owner B; unauthenticated API requests are denied; deletion behaviour when approved.

## Privacy, retention, and logging

No CV file bytes, bearer tokens, OIDC tokens, emails, or real fixtures are stored in this increment. Structured logs may include a correlation ID, operation, result category, profile ID, version, and duration; they must not include issuer, subject, claims, headline, evidence, source references, request bodies, or database connection strings.

Proposed initial policy: retain active profile snapshots and evidence only while the account is active; an approved deletion request hard-deletes the owner's profile graph in one controlled operation. Backups must expire within an approved, documented timeline and restoration must not silently reactivate deleted data. The precise deletion workflow and backup-retention period remain human approval items.

## Delivery order

1. #32: approve this design and ADR-0012 (documentation only).
2. #33: Application identity, ports, and use cases.
3. #34: EF Core 10 SQL Server Infrastructure, migrations, and isolated SQL integration tests.
4. #35: owner-scoped API endpoints and authorization tests.
5. Profile UI is a later, separately scoped Blazor issue after #35 passes.

## Non-goals

No profile UI, database implementation, `DbContext`, migrations, endpoints, CV parsing, multi-profile support, automatic applications, Dapper, or generic repository is introduced by #32.
