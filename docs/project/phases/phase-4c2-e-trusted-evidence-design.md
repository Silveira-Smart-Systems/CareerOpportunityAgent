# Phase 4C.2-E — Trusted evidence and authorization design (P0)

Tracking: #74. Depends on #72 / Draft PR #73. **Design only; no implementation or provider activation.**

## Problem and trust boundary

A source adapter is untrusted for authorization. Its publication-country claim, URL, external identifier, and free-form evidence strings are observations, **not** authorization. A planned eligible scope is also not permission to execute. The scoped execution gate must validate current authority-controlled evidence at reservation time.

## Proposed model (for review, not committed API)

- **Evidence subject:** immutable `SourceId` + exact `SourceExecutionScope` discriminated variant. `LegacyMarket(PT)` must never be treated as `PublicationCountry(PT)`.
- **Evidence authority:** trusted internal registration/verification authority; record immutable evidence ID, issuer, verification timestamp, expiry, revocation status and evidence version. External provider metadata cannot set these fields.
- **Verification status:** `Unknown`, `Pending`, `Verified`, `Rejected`, `Revoked`, `Expired`. Only `Verified` with current validity and no revocation may satisfy the evidence condition.
- **Policy evaluation:** evidence verification is necessary but not sufficient. Licensing, source availability, budget, quota and capacity are independently checked by the gate against current policy.
- **Decision:** deny with a stable reason code, or reserve a permit atomically for the exact target and attempt. A reason code must not leak secrets or evidence payloads.

## Proposed decision flow

1. Accept exact requested target and immutable search criteria from the validated execution plan.
2. Look up authority-owned registration for the exact `SourceId` and scope variant; missing or ambiguous records deny.
3. Check verified status, issuer trust, expiration, revocation and version freshness. Deny when any check is unavailable or inconclusive.
4. Re-evaluate current license/policy, health, quota, budget and capacity.
5. Atomically reserve capacity and budget, producing a single-use permit bound to target and attempt ID.
6. Mark attempt started before calling the adapter. Consume after start, even on timeout/cancellation/failure; release only if execution never started. Retries require a new policy decision/reservation.
7. Treat any returned provider claims as observations; do not update evidence authority or grant permissions from them.

## Threats and mitigations

| Threat | Mitigation |
| --- | --- |
| Provider forges `verified` or `country=PT` | Authority-owned verification, no provider write path |
| Evidence revoked between planning and execution | Fresh validation at reservation time; fail closed |
| Expired/missing evidence or authority outage | Deny; never reuse stale planning evidence |
| Scope confusion `LegacyMarket(PT)` vs `PublicationCountry(PT)` | Exact discriminated scope identity in evidence and permit |
| Concurrent instances oversubscribe quota/budget | Transactional compare-and-reserve; distributed design required |
| Replay of permits or duplicate attempts | Single-use permit + idempotent attempt identifier and durable accounting |
| Secrets leaked through trace or errors | Redacted diagnostics, opaque evidence IDs, no raw tokens |

## Contract and compatibility proposal

Keep `IScopedSourceExecutionGate` and `ScopedSourceExecutionAuthorization` as the orchestration boundary. Introduce an authority-backed evaluator behind the gate rather than attaching a `Verified` boolean to `ScopedSourceProvenance`. Maintain existing diagnostic reason codes where possible; add only explicitly versioned, additive reasons. Existing in-memory gate remains a test/reference implementation.

## Acceptance test matrix

- Exact verified and current evidence + policy + capacity: grant once.
- Unknown, pending, rejected, expired, revoked, absent or untrusted issuer: deny.
- Evidence valid for `LegacyMarket(PT)` but request `PublicationCountry(PT)`: deny.
- Planning-time valid, execution-time revoked or expired: deny.
- Provider reports matching PT or conflicting ES geography: authorization unchanged.
- Evidence service unavailable or stale: deny.
- License denied, source unhealthy, budget/quota exhausted: deny independently of evidence.
- Two concurrent requests for final capacity unit: at most one reservation.
- Timeout, cancellation, adapter error, retry and duplicate consume: preserve durable permit accounting.
- Diagnostics: stable non-sensitive reason codes; no credentials or sensitive search values.

## Open decisions for architecture review

1. Who owns trusted verification and what evidence classes can they verify?
2. What expiry/revocation freshness guarantee and consistency model are required?
3. Which durable store/transaction boundary owns quota, budget and reservations?
4. How should reservations recover from process crashes and uncertain starts?
5. What audit retention and access controls apply to authority decisions?

## Explicit non-goals

No licensing determination, Jobicy/provider activation, scheduler, ingestion, persistence migration, production rollout, or merge authorization. Later workstreams for field-level trace, deduplication and distributed gate remain tracked in #74.

## coa-004 architectural review — proposed resolutions (not implementation approval)

Review: Draft PR #75, review 6013011318; tracking #74, parent #67, dependency #72 / Draft PR #73. These are **design constraints and proposals**, not evidence of implemented distributed guarantees.

### Authority, verification and consistency boundary (P0)

- Only an internally controlled evidence registry and verifier may issue, update or revoke verification records. Provider claims, URLs, external IDs, and `ScopedSourceProvenance` remain untrusted observations.
- The verification record is bound to an exact typed `(SourceId, SourceExecutionScope)`, with an opaque evidence ID, trusted issuer, verification version, verified-at, expiry, and revocation state. No implicit conversion between scope variants.
- Proposed invariant: a reservation is allowed only when a current, authoritative evidence version and current policy are validated within a consistency boundary that prevents a concurrent revocation from being silently ignored. The required transactional/linearization mechanism remains **open pending backend selection**. A cached or planning-time `Verified` value alone never authorizes.
- If authority freshness, version comparison, policy consistency, or revocation status cannot be established, **deny**. A revocation racing with reservation must have a defined ordering: if revocation precedes authorization's linearization point, deny; otherwise the treatment of already reserved or started attempts is an explicit operational policy decision, not an implicit guarantee.
- Evidence verification is necessary, never sufficient: current licensing policy, health, quota, budget and capacity must independently pass.

### Authorization contract versus deployment implementation (P0)

- Retain `IScopedSourceExecutionGate` / `ScopedSourceExecutionAuthorization` as the compatibility boundary; evolve additively. A permit must be bound to exact typed target, attempt ID, authority evidence version and decision context, without exposing sensitive evidence.
- The existing in-memory gate is **single-process test/reference only**. It does not provide cross-instance atomicity, crash durability or production authorization guarantees.
- Production-grade compare-and-reserve for budget/quota/capacity requires a separately designed durable transaction boundary and a separately authorized implementation. Until then, no production or multi-instance activation.

### Attempt lifecycle and crash recovery (P0)

Proposed logical states: `Reserved -> Started -> Consumed`, or `Reserved -> Released` **only with authoritative proof that execution never started**. `Started -> Consumed` includes timeout, cancellation and adapter failure. Unknown/crash states are conservatively treated as potentially started, never automatically released or replayed.

- Persist/coordinate the attempt identity and single-use permit before the adapter is invoked; exact ordering, durability and recovery mechanism are pending distributed-gate design.
- Repeated consume/release must be idempotent. A retry is a new decision and reservation with an explicit attempt identity; no reuse of a consumed or uncertain permit.
- Cancellation before proven start may release; cancellation after or with uncertain start consumes/holds conservatively. Recovery/reconciliation requires auditable operator or authority-owned rules; no automatic double-spend.

### Denial diagnostics and audit (P1 specification)

- Proposed additive, versioned reason categories: `EvidenceMissing`, `EvidenceNotVerified`, `EvidenceExpired`, `EvidenceRevoked`, `EvidenceStale`, `EvidenceAuthorityUnavailable`, `ScopeMismatch`, `PolicyDenied`, `SourceUnavailable`, `QuotaExceeded`, `BudgetExceeded`, `CapacityUnavailable`, `ReservationConflict`. Final naming and compatibility mapping require contract review.
- Audit the decision timestamp, opaque evidence/version reference, exact typed scope, attempt/correlation ID, outcome and safe reason category; redact secrets and sensitive criteria. Do not log raw provider evidence, credentials, sensitive URLs or token-bearing query strings.
- Retention, access control, deletion policy and audit storage ownership remain **open**, requiring explicit approval before production design sign-off.

### Deterministic acceptance and negative test requirements

| Scenario | Expected invariant |
| --- | --- |
| Missing, pending, rejected, expired, revoked or untrusted evidence | Deny before adapter invocation |
| Evidence authority unavailable, stale version, unverifiable revocation | Deny; no fallback to cached or provider claim |
| Revocation racing with reservation | Respect defined authorization ordering; never authorize after an earlier committed revocation |
| `LegacyMarket(PT)` evidence for `PublicationCountry(PT)` request | Deny exact-scope mismatch |
| Provider geography agrees, disagrees or is unknown | No change to authorization decision |
| License/health/quota/budget/capacity fails despite verified evidence | Deny |
| Two concurrent final-slot reservations | At most one grant in a future durable implementation |
| Duplicate permit consume or repeated retry | No double accounting or permit replay |
| Crash before confirmed start | Release only if non-start is provable |
| Crash, timeout or cancellation after/around uncertain start | No unsafe release; conservative accounting and reconciliation |
| Diagnostic/audit output | Stable safe reason; no credentials or raw sensitive evidence |

### Delivery and approval gates

1. **Evidence contract and verification policy**: independently reviewable design and negative tests; no runtime changes without separate authorization.
2. **Scoped gate integration and attempt lifecycle**: additive compatibility, explicit regression and failure tests; separate implementation approval.
3. **Durable distributed reservation**: backend selection, concurrency/recovery guarantees, operations and audit policies; separate design, implementation and deployment approvals.

Outstanding architecture decisions: trusted verifier ownership and eligible evidence classes; revocation linearization and post-reservation policy; durable transaction backend; uncertain-start reconciliation; audit retention/access ownership. None is silently treated as settled by green CI.

**Guardrails:** keep #73/#75 Draft until explicit review/merge authorization; Jobicy disabled; #47 out of scope; no public HTTP, ingestion, scheduler, persistence migration, provider activation, deployment or runtime implementation from this document.


## coa-004 — ADR candidate: revocation/reservation ordering (proposed, not accepted)

**Status:** Proposed for architecture review. This is not an implementation instruction or a claim of current system behavior.

### Decision drivers

- A verified record may be revoked after planning, during gate evaluation, or after a permit has been reserved.
- Multiple workers must not independently authorize using stale cached evidence or oversubscribe a shared budget.
- The adapter may have been invoked when a worker crashes; releasing uncertain permits can authorize duplicate execution.

### Proposed authorization ordering

1. Identify the exact typed target and new idempotent attempt ID; validate the immutable plan.
2. Read the current authority-owned evidence/version and revocation state through a strongly consistent authority interface. Reject missing, ambiguous, expired, stale or unavailable state.
3. Re-evaluate policy and quota/budget/capacity against the same authoritative decision epoch. The eventual production transaction must **serialize reservation with revocation and relevant policy updates**, or use an equivalent fenced/version-checked protocol with a documented proof of correctness. No check-then-write gap may silently authorize after an earlier committed revocation.
4. Commit a single-use reservation with the evidence version, policy epoch, target and attempt identity. The successful commit is the **authorization linearization point**; it must be ordered with revocation.
5. Before invoking the adapter, durably transition the attempt to `Started` under the same attempt identity. If the start transition is uncertain, treat the attempt as potentially started; do not release or replay automatically.
6. After the adapter returns or fails, settle the attempt idempotently. Cancellation, timeout and failure after `Started` consume the reservation. Reauthorization is required for a retry.

### Revocation timing semantics

| Timing | Proposed treatment |
| --- | --- |
| Revocation committed before authorization linearization | Deny |
| Revocation concurrent with reservation | Exactly one ordering must be authoritative; deny if ordering cannot be established |
| Revocation committed after reservation but before confirmed adapter start | **Open policy choice:** cancel and safely release only with proven non-start, or permit the already-authorized attempt |
| Revocation after adapter start | **Open policy choice:** allow attempt to settle or attempt best-effort cancellation; do not retroactively pretend it never started |
| Authority outage or unknown version during authorization | Deny, no cached fallback |

### Alternative analysis

- **Transactional serialization** across evidence status, policy epoch and reservation: clearest safety semantics, but may require co-location or coordinated transaction boundaries.
- **Fenced optimistic concurrency** with monotonic authority version and compare-and-reserve: can avoid a single storage transaction if correctness is demonstrated for revocation races and all writers honor fencing.
- **Eventually consistent cache-only validation:** rejected for authorization; insufficient to guarantee fail-closed behavior during revocation.

**Recommendation:** choose a linearizable decision boundary as the *required property*, not a specific database or distributed lock. Select storage and prove the ordering contract in the separate distributed-gate design. Do not assert that the current in-memory implementation meets it.

### Required design sign-off before implementation

- Name the trusted authority owner and which evidence classes it can verify.
- Decide post-reservation and post-start revocation policy, including whether already-started attempts may continue.
- Select a durable consistency mechanism and document failure behavior, fencing/versioning and crash recovery.
- Define audit retention, access ownership and safe reason-code mapping.
- Add concurrency model tests that force revocation/reservation interleavings and uncertain start, beyond normal CI success.

**No change to approval boundaries:** PR #75 remains Draft; no provider activation, implementation, merge or deployment follows from this proposed ADR.


## coa-004 — ADR candidate: revocation after reservation and attempt start

**Status: Proposed; requires explicit architecture sign-off.** Applies to future authority-backed execution, not the current in-memory reference gate.

### Recommended policy

- **Reserved, not started; revocation committed:** block adapter start. Cancel the reservation and release only when a durable, authoritative state transition proves the attempt never started. Otherwise quarantine/hold for reconciliation and account conservatively.
- **Start requested concurrently with revocation:** start authorization must perform a fenced, authoritative transition that is ordered with revocation. A prior committed revocation wins and prevents start; if the ordering is unknown, no new adapter invocation is permitted.
- **Started before revocation:** mark the attempt as potentially billable/consumed and do not reissue the permit. Where cancellation is supported, request best-effort cancellation; cancellation does **not** establish that the external side effect was prevented. Settle conservatively, audit the revocation and the outcome, and never retroactively release solely because the adapter returned an error.
- **Revocation during unknown/crash state:** prevent retry on the old permit, retain conservative accounting and require authority-owned reconciliation with auditable evidence of non-start before any release.
- **Future attempts:** always require a new authoritative verification and reservation; revoked evidence denies.

### Explicit separation of state and accounting

`Reserved`, `Started`, `Consumed`, `Released` and `ReconciliationRequired` are **proposed logical states**, not existing implementation types. The lifecycle must be monotonic, idempotent and durable. A record can be marked `ReconciliationRequired` without granting a second execution. Reconciliation must not invent a successful external cancellation.

### Required concurrency tests

1. Revocation commits before a fenced `Reserved -> Started` transition: no adapter call.
2. Start commits first, then revocation: one attempt maximum, conservative consumption, optional best-effort cancellation.
3. Worker crashes after reservation but before provable start: no unsafe release; reconcile.
4. Worker crashes after durable start but before adapter response: no retry/release on same permit.
5. Duplicate revocation, settlement and reconciliation events: idempotent transitions and single accounting.
6. Authority outage while transitioning to start: no adapter call, hold/deny pending reconciliation.
7. New attempt after revocation: deny even when an older permit existed.

### Architecture decisions still required

- Which internal team/service owns authoritative evidence verification, revocation and reconciliation?
- What persistence/transaction protocol proves the ordering of revocation, reservation and start?
- Is best-effort cancellation mandated for all adapters or only adapters with explicit support?
- What retention, access and incident escalation policies govern reconciliation records?

The recommended behavior is a **design proposal**, not a production guarantee, and does not authorize runtime changes, merges, deployment or provider activation.
