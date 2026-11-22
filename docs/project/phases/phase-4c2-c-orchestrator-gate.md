# Phase 4C.2-C — Scoped Orchestrator & Gate

Parent: [#67](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67). Depends on merged [#70](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/70).

## Architectural checkpoint

The legacy orchestrator, adapter, gate, permit and outcome types use `CountryCode Market` and `SourceExecutionTarget`. The new scoped planner emits `ScopedSourceExecutionTarget` with mutually distinct `LegacyMarket`, `PublicationCountry` and `UnconstrainedPublication`. **Do not cast, convert or infer one scope from another.**

### Incremental implementation
1. Introduce additive scoped execution contracts: scoped adapter, resolver, gate, permit and outcome identities. No changes to legacy interfaces.
2. Require an explicit request/plan match and fail-closed plan validation (unique source/scope targets, mandatory criteria, declared capability, no widened publication scope). The plan is not a permit.
3. Add an atomic scoped `AuthorizeAndReserveAsync` boundary that rechecks current scope capability/evidence registration, authorization, licensing, health, quota, budget and temporary restrictions. The gate owns reservation; do not trust planning-time snapshots as current authorization.
4. Add scoped orchestrator with bounded concurrency, cancellation, timeout and late-task slot safety; preserve the legacy permit lifecycle semantics, including exactly-once start/consume or release behavior.
5. Verify adapter results against scoped provenance and applied mandatory criteria. A publication-country scope must not be reported as a legacy requested market. Defer comprehensive provenance evolution to 4C.2-D.
6. Test denied/missing/unknown scopes, concurrency, races, cancellation, timeout, failed gate, permit release/consume, result validation, deterministic ordering, and legacy regression. Review CI before approval.

### Security and correctness risks
- **TOCTOU:** planning eligibility is advisory; gate must atomically revalidate and reserve before execution.
- **Evidence:** an `EvidenceReference` string is not verified evidence. The registration boundary must establish verified capabilities independently.
- **Semantic leakage:** old `SourceProvenance.RequestedMarket` cannot represent unconstrained publication; introduce scoped provenance rather than fabricating a country.
- **Late operations:** timeout must not release concurrency slots while adapter work is still running.
- **Cancellation:** preserve permit accounting and avoid double release/consume.
- **Compatibility:** existing `DiscoveryOrchestrator` and its interfaces remain unchanged until regression coverage proves a safe migration.

## Scope limits
Jobicy stays disabled. No provider HTTP activation, no scheduler, ingestion, persistence, global deduplication or issue #47 work. No automatic merge. GitHub remains the source of truth.

## Status
**Implementation checkpoint (2026-11-22):** Scoped contracts, validator, gate, in-memory reservation authority, orchestrator, and regression tests are implemented in Draft PR #71. CI for commit `2198fb4` completed successfully (workflow run `45028309425`). This does **not** constitute production approval or merge authorization.

### Reference authority boundary (not production-ready)

- `InMemoryScopedExecutionReservationAuthority` serializes authorization and slot reservations **only within a single process**. It is not a distributed or durable authority; concurrent pods could independently grant permits.
- `RegisterVerified` accepts caller-asserted verified scopes and an immutable `SourcePlanningState`. It does not independently fetch, validate, expire, or refresh evidence. An evidence-reference string alone must never be treated as proof.
- Authorization, licensing, health, quota, budget and restrictions are checked against **registered snapshots**, not live provider/central state. Consequently, revocations or changing quotas after registration are not automatically reflected.
- A permit marked started is consumed even when the adapter fails, times out, or the caller cancels. Only a permit that never started may be refunded. Timed-out/noncooperative adapter tasks retain their concurrency slot until completion; this protects local limits but cannot guarantee termination of an uncooperative adapter.
- The scoped gate and reference authority are useful for contract validation and tests, **not** for enabling production source execution.

### Production prerequisites / remaining acceptance gates

1. Introduce a shared transactional reservation authority (durable atomic quota/budget reservation, cross-pod idempotency and concurrency guarantees) with clear reservation expiration/recovery semantics.
2. Establish independently validated, time-bounded evidence for each source/scope and revalidate authorization, licensing, runtime restrictions and capacity at execution time; fail closed on unknown/stale/revoked state.
3. Exercise cancellation and timeout races, permit state transitions and cross-instance contention against the production authority; confirm auditability and metrics.
4. Review all CI jobs and the PR diff, then request explicit human approval before any merge. Jobicy remains disabled; issue #47 remains excluded.

**Decision:** Keep PR #71 Draft. Passing CI validates the tested reference implementation, not distributed production readiness.
