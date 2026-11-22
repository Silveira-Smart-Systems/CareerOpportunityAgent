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