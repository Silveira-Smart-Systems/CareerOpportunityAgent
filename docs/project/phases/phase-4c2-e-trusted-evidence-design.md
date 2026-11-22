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


## coa-004 — ADR candidate: evidence authority and reconciliation ownership

**Status: Proposed; organizational ownership, infrastructure and implementation are not approved.**

### Proposed separation of responsibilities

| Responsibility | Logical owner | Allowed actions | Prohibited actions |
| --- | --- | --- | --- |
| Evidence verification | Internal evidence authority / verifier | Register independently checked evidence; issue immutable evidence versions; revoke or expire verification; record issuer and provenance of verification | Accept provider-supplied `verified` or geography as authorization; reserve execution budget |
| Execution authorization | Scoped gate / policy decision service | Validate exact target and current authority version; evaluate license, health, budget, quota, capacity; atomically authorize/reserve attempt | Change verification status; accept a planning-time decision as permission |
| Execution | Orchestrator and source adapter | Use only a valid single-use permit; transition attempt to started before adapter invocation; emit observations | Grant or renew own permit; write trusted evidence or override revocation |
| Reconciliation | Internal attempt-accounting authority | Settle ambiguous/crashed attempts from durable state and auditable evidence; authorize release only on proven non-start | Assume a timeout means non-execution; silently replay or release uncertain permits |
| Governance and audit | Designated product/security/data owners (to be named) | Approve eligible evidence classes, issuer trust, licensing policy, retention, access and escalation | Delegate trust decisions to unreviewed provider metadata |

These are **logical boundaries**, not a requirement for five deployable microservices. One process may host multiple roles only with explicit authorization controls and independent write permissions.

### Evidence classes and provenance of verification

- **Provider observations** (country claim, URL, external ID, free text) are never authority-issued evidence.
- **Internally verified source eligibility** may become authority-owned evidence only after a defined, auditable verification procedure with named issuer, exact typed scope, verification timestamp, expiry, version and revocation capability.
- **Licensing/terms permission** requires a separately owned policy determination; a valid source identity or geography claim alone never establishes legal rights.
- **Operational readiness** (health, capacity, budget, quota) is current gate policy state, not durable proof of provider eligibility.
- Unknown evidence classes and untrusted issuers fail closed until explicitly approved.

### Authority write and read boundaries

- Providers and adapters have read-only or no access to trusted verification; they must not create, mutate or revoke authority records.
- Only designated verifier identities may issue or revoke evidence; changes are versioned, authenticated and audited.
- The gate has authoritative read/evaluate rights and reservation rights, not evidence-issuance rights.
- Reconciliation has narrowly scoped settlement rights and must preserve append-only audit lineage. No manual release without proof of non-start and recorded actor/rationale.
- The eventual production protocol must provide an ordering guarantee between revocation, authorization and start transitions; ownership separation must not introduce an unchecked distributed race.

### Reconciliation policy proposal

1. A durable attempt record identifies permit, typed target, evidence version, authorization epoch, state and safe correlation ID.
2. If non-start is proven by authoritative durable transition history, release exactly once.
3. If `Started` is recorded or start status is uncertain, do not release or retry the permit; hold or consume conservatively according to approved accounting rules.
4. Escalate ambiguous records through a bounded, auditable workflow with least-privilege reviewer access. Do not claim that an external provider did not execute solely because it timed out.
5. New attempts must always reauthorize using current evidence and policy.

### Explicit decisions required from product/architecture owners

1. Name the real owner(s) of verifier, authorization policy and reconciliation; decide if these are modules or separate services.
2. Approve the initial eligible evidence classes, verification procedure, issuer trust and revocation triggers.
3. Choose the authoritative durable store/protocol and define cross-role transaction/fencing guarantees.
4. Specify operational escalation, reconciliation SLA, retention, access controls and incident handling.
5. Approve the final denial-reason taxonomy and any compatibility mapping.

### Test obligations

- Provider and adapter identities cannot issue or alter verified evidence.
- An issuer with insufficient privilege cannot verify/revoke another authority's record.
- Gate denies unknown class, untrusted issuer, stale version, missing revocation proof and unavailable authority.
- Reconciliation cannot release an attempt with uncertain or recorded start; duplicate settlement is idempotent.
- Revocation/authorization/start interleavings remain correctly ordered across roles and workers.
- Audit events contain actor, decision, opaque evidence reference, version and reason without secrets.

**Delivery boundary:** proposed ownership and tests only. No new service, persistence, provider activation, implementation, merge or deploy authorized.


## coa-004 — P0 architecture decision register and readiness gate

**Status:** Consolidated review proposal; no final architecture approval. This register separates mandatory safety properties, recommended MVP direction and decisions requiring human ownership.

| ID | Topic | Required invariant / boundary | MVP recommendation | Approval status |
| --- | --- | --- | --- | --- |
| P0-01 | Trust | Provider data and requested plan never authorize; exact typed source/scope | Internal verifier interface and versioned evidence records | Invariant documented; owner/procedure open |
| P0-02 | Revocation | Fail closed on stale, missing, revoked or unavailable evidence; no check-then-reserve race | Linearizable authorization boundary, using transaction or proven fencing | Property proposed; backend/proof open |
| P0-03 | Post-reservation revocation | No new start after earlier committed revocation; no unsafe release for uncertain start | Fenced start transition; conservative settlement | Proposed policy; owner sign-off open |
| P0-04 | Roles | Issuer, gate, adapter and reconciliation cannot self-authorize | Modular boundaries with least-privilege writes; no premature microservices | Logical design proposed; actual owners open |
| P0-05 | Capacity | At most one grant for final shared slot; idempotent permit accounting | Durable compare-and-reserve in later dedicated workstream | Production implementation deferred |
| P0-06 | Audit | Safe reason codes and trace without sensitive payloads | Opaque evidence/version refs and actor/attempt audit trail | Retention/access/taxonomy open |
| P0-07 | Compatibility | Existing scoped interfaces and legacy behavior remain intact | Additive contracts and explicit versioning | Regression tests required |
| P0-08 | Rollout | No provider activation or distributed claims before proof and review | Test/reference gate only in current scope | Explicitly blocked |

### Recommended MVP implementation sequencing (requires separate approval)

1. **Contract-only increment:** typed evidence subject and states, verifier interface, safe reason taxonomy and negative unit tests; no provider integration.
2. **Gate policy increment:** authority-backed decision path, revocation/freshness denial, permit and attempt state contracts, backward compatibility tests; no production activation.
3. **Distributed readiness increment:** durable reservation backend, fencing/linearization proof, crash reconciliation, multi-worker concurrency tests and operational audit; separately reviewed.
4. **Operational readiness:** named owners, approved evidence classes and licensing policy, retention/access controls, runbooks, load/failure tests and explicit release authorization.

### Architecture sign-off checklist

- [x] Trust boundary, exact typed scope and fail-closed principles documented
- [x] Proposed revocation/reservation ordering and alternative mechanisms documented
- [x] Proposed post-reservation/start policy and crash-accounting invariants documented
- [x] Logical ownership and permission boundaries proposed
- [x] Negative, race, idempotency and audit test requirements documented
- [ ] Product/security owner confirms eligible evidence classes, verifier authority and licensing policy
- [ ] Architecture owner approves revocation semantics and consistency proof approach
- [ ] Technical owner selects durable store, recovery protocol and multi-instance acceptance tests
- [ ] Governance owner approves retention, access, incident escalation and denial taxonomy
- [ ] Explicit approval for any implementation, PR merge or deployment

### Recommendation for next human checkpoint

Request one architecture decision covering (a) authority ownership and initial evidence classes, (b) revocation-after-reservation policy, and (c) durable consistency mechanism selection or a bounded proof-of-concept decision. Do not describe green documentation CI as proof of these production properties.

**Current outcome:** documentation is ready for a structured architecture review; P0 is **not signed off**, and all runtime/merge/deployment restrictions remain.


## coa-004 — Human decision record: conceptual P0 direction

**Decision:** Conceptual architectural direction approved by the product owner in the COA conversation on 2026-11-22, following presentation of the P0 decision register. **Scope: principles and component responsibilities only.**

Approved conceptual direction:
- Maintain provider-supplied observations separate from trusted, authority-owned evidence.
- Apply fail-closed, exact-scope authorization and versioned revocation checks.
- Separate logical evidence verification, gate authorization, adapter execution, reconciliation and governance/audit responsibilities; no mandate for separate deployable services.
- Treat post-reservation revocation and uncertain execution conservatively, subject to final operational semantics and proofs.
- Require durable and correctly ordered distributed reservations before any production claim or rollout.
- Prepare design contracts and task breakdown as **documentation only**, pending distinct implementation authorization.

**Not approved by this decision:** concrete authority owner/issuer classes, licensing or legal determinations, database or consistency protocol, reconciliation SLA, audit retention/access taxonomy, runtime code, provider activation, PR merge or deployment.

**Gate:** Conceptual direction = approved; detailed P0 operational/implementation readiness = pending. CI #134 succeeded for prior commit `a945ee6`; any subsequent commit requires its own validation. A green documentation CI is not evidence of distributed safety.


## coa-004 — Proposed contract backlog and dependency order (documentation only)

**Status:** Planning approved for documentation; contract names are illustrative and not committed API/schema types. No implementation authorization.

### Proposed contracts

| Contract / boundary | Minimum semantics | Consumer / owner | Acceptance evidence |
| --- | --- | --- | --- |
| `EvidenceSubject` | Exact provider/source and typed scope, including non-equivalence of `LegacyMarket(PT)` and `PublicationCountry(PT)` | Internal verifier; gate | Cross-scope negative tests |
| `VerifiedEvidenceRecord` | Opaque ID, subject, approved class, trusted issuer, version, issued/expiry/revoked status and verification provenance | Authority-owned writes; gate reads | Untrusted issuer, stale/unknown/revoked denial |
| `EvidenceDecision` | Eligible/ineligible/unknown; safe reason codes; observed version and decision timestamp | Gate | Fail-closed and no provider-controlled authority |
| `ReservationRequest/Permit` | Exact target, authority/policy version, unique attempt and single-use fenced token; idempotency | Gate / reservation authority | Atomic reservation and concurrent-winner tests |
| `AttemptLifecycle` | Durable reserved/started/settled/reconciliation-needed transitions; no implicit retry | Executor and reconciliation authority | Crash/replay/race tests |
| `ReconciliationOutcome` | Proof-backed release or conservative settlement; actor, reason and immutable lineage | Reconciliation authority | Unknown-start never released without proof |
| `AuthorizationAuditEvent` | Opaque correlation, subject, evidence version, actor, safe reason and transition | Audit governance | Secret redaction and audit consistency tests |

These are logical responsibilities; do not infer separate microservices or a particular storage technology. Do not expose full source content, credentials or provider payloads in authorization/audit events.

### Dependency-first documentation work packages

| Order | Work package | Depends on | Exit criteria (design) |
| --- | --- | --- | --- |
| D0 | Approve owner matrix and evidence classes | Conceptual P0 approval | Named owners, issuance/revocation procedure and licensing decision path |
| D1 | Specify typed evidence and safe decision contracts | D0 | Versioned schemas, invariants, backward-compatibility mapping and negative test cases |
| D2 | Specify permit/attempt state machine | D1 | Fenced start, idempotency, unknown-start policy and race diagrams |
| D3 | Evaluate durable authority and reservation consistency options | D1–D2 | Written ordering proof approach, failure model and backend selection decision |
| D4 | Specify reconciliation, audit and operations | D2–D3 | Evidence-of-non-start standard, safe taxonomy, retention/access, runbooks and escalation |
| D5 | Prepare implementation issues and rollout gates | D0–D4 | Small scoped acceptance criteria, owners, regression/concurrency matrix and explicit implementation approval |

**Parallelizable documentation:** D1 contract vocabulary and D0 owner discovery may be drafted concurrently, but D1 cannot be finalized before D0; D3 option analysis may begin while D2 is drafted, but cannot be accepted before the state machine is agreed.

### Scope guards and review checkpoints

- Keep PR #75 Draft and use issue #74 as the decision trail; do not create implementation branches from unapproved schemas.
- PR #73's in-memory gate remains reference/test-only, never production distributed authority.
- Any provider observation, including Jobicy geography, cannot become verified evidence by copying a claim.
- No scheduler, ingestion, provider activation, licensing assumption, runtime code, database migration, merge or deploy is authorized.
- Green CI on a documentation commit verifies the repository checks, not the correctness of production distributed authorization.
- **Next human decision:** identify authority/governance owners and evidence classes; approve a durable ordering approach or a bounded evaluation plan before implementation.


## coa-004 — Contract acceptance criteria and adversarial test catalog

**Status: Proposed test specification only.** The test IDs below are traceable acceptance criteria, not implemented or executed tests. All production guarantees require a separately approved durable design and multi-worker verification.

| Test ID | Boundary | Given / when | Required observable result |
| --- | --- | --- | --- |
| EV-01 | Typed evidence subject | `LegacyMarket(PT)` evidence is presented for `PublicationCountry(PT)` | Deny; no implicit conversion or permit |
| EV-02 | Evidence issuer | Provider/adapter supplies `verified=true` or external ID | Treat as observation; deny absent independent authority verification |
| EV-03 | Evidence validity | Evidence missing, unknown class/issuer, expired, stale version or revoked | Fail closed with safe, non-sensitive reason |
| EV-04 | Evidence authority | Unauthorized identity attempts issue/revoke/overwrite | Reject and audit without changing trusted state |
| AU-01 | Authorization gate | Authority unreachable or version cannot be proven current | No reservation and no adapter invocation |
| AU-02 | Atomic reservation | Two workers race for final shared capacity slot | At most one durable successful reservation |
| AU-03 | Idempotency | Same attempt request replayed concurrently | At most one permit and one capacity charge |
| AU-04 | Typed target | Permit is replayed against different source/scope | Deny; permit never widens authority |
| RV-01 | Revocation ordering | Revocation commits before reservation | No reservation or adapter start |
| RV-02 | Fenced start | Revocation commits after reservation but before fenced start | Block start; release only with authoritative non-start proof |
| RV-03 | Concurrent transition | Revocation and start race across workers | Deterministic order; no start after earlier committed revocation |
| RV-04 | Post-start revocation | Start commits before revocation | No replay; conservative settlement; optional supported cancellation does not prove non-execution |
| AT-01 | Crash recovery | Worker crashes after reservation before proven start | No automatic unsafe release; reconciliation required |
| AT-02 | Unknown external result | Worker crashes/times out after durable start | No automatic retry or release on same permit |
| AT-03 | Settlement | Duplicate completion/revocation/reconciliation events arrive | Idempotent state and single accounting |
| AT-04 | Release | Reviewer requests release with no authoritative non-start evidence | Deny release and record audit rationale |
| AD-01 | Audit safety | Provider payload contains token/secret/PII | Logs omit secrets and sensitive payload; retain opaque correlation and safe reason |
| AD-02 | Audit completeness | Authority version changes during attempt | Actor, evidence version, decision, state transition and safe reason are traceable |
| BC-01 | Compatibility | Existing legacy-scoped consumer uses unchanged interface | No silent semantic change; regression tests pass |
| OP-01 | Rollout guard | Provider disabled and feature gate off | No scheduling, ingestion, adapter invocation or production authorization |

### Test execution tiers and evidence required

- **Tier A — pure contract/unit:** typed-scope non-equivalence, trust provenance, reason mapping, state-machine invalid transitions, serialization compatibility.
- **Tier B — authority integration:** issuer permissions, stale/revoked evidence, durable version/fencing behavior, audit writes and crash-restart recovery.
- **Tier C — multi-worker deterministic concurrency:** barrier-controlled revocation/start races, last-slot contention, replay/idempotency and reconciliation races across processes.
- **Tier D — operational failure injection:** database/authority outage, partial commit, worker restart, adapter timeout, delayed response, duplicate delivery and rollback/feature-off verification.

Every test report must record the exact commit, environment, backend, test seed, number of workers, fault schedule and observed audit/permit outcomes. Passing Tier A does not satisfy Tier C/D.

### Proposed definition of done for implementation issues (future, not authorized)

1. Contract and exact typed-scope behavior reviewed by the designated owner.
2. Negative-path acceptance cases mapped to tests with no missing security assertions.
3. No provider-origin field can issue authority or bypass deny conditions.
4. Distributed correctness claims supported by repeatable multi-worker tests against the selected durable backend.
5. Safe audit, operational recovery, rollback and compatibility evidence attached to PR.
6. Explicit human sign-off before merge, provider enablement or deployment.

**Unresolved prerequisites:** D0 named owners and evidence classes, backend consistency proof, audit retention/access, operational reconciliation rules and separate implementation approval.


## coa-004 — D0 proposed accountable roles and initial evidence-class registry

**Status: Proposal for product/architecture/governance approval; not a delegation of authority.** Names below are functional roles, not appointed people or provisioned service identities.

### Proposed MVP responsibility assignment

| Role | Accountable decision | Proposed logical boundary | Required approval before activation |
| --- | --- | --- | --- |
| Product owner | Eligible sources, scope of discovery and business risk acceptance | Product governance | Names the actual responsible person and approves evidence classes |
| Evidence policy owner | Trust criteria, accepted issuers, verification procedure, expiry and revocation | Internal evidence-policy module | Product + security/data governance sign-off |
| Evidence verifier | Performs independent checks and records versioned evidence | Restricted authority writer | Identity, credentials and audit controls approved |
| Authorization gate owner | Exact-scope policy evaluation, permit reservation and fail-closed behavior | Gate policy module | Architecture owner approves consistency and fencing proof |
| Reconciliation owner | Resolves uncertain starts and permit accounting | Restricted attempt-ledger workflow | Operations/security owner approves proof, escalation and access |
| Audit/data governance owner | Event schema, safe reasons, access, retention and deletion | Audit policy boundary | Named privacy/security/data owner approves policy |

One accountable human may hold multiple governance roles initially, but execution identities and write privileges must remain separated. These role labels **do not grant permissions**.

### Proposed initial evidence classes and eligibility

| Candidate class | What it can establish | What it cannot establish | Default status |
| --- | --- | --- | --- |
| `VerifiedSourceIdentity` | An independently checked provider/source identity and exact typed target | License to ingest, scrape, republish or call APIs | Candidate; not issuable until procedure approved |
| `VerifiedScopeApplicability` | Evidence that a defined source-specific policy applies to a typed target | That a provider's self-reported country is true or that all countries are equivalent | Candidate; not issuable until procedure approved |
| `ApprovedAccessBasis` | A separately reviewed permission basis for a precisely defined operation, source and time window | Broad rights beyond the reviewed operation or future policy changes | Candidate; requires explicit licensing/terms determination |
| `OperationalReadiness` | Current health/quota/budget signals used by the gate | Trusted legal/identity evidence or durable eligibility | Gate input only; **not** an evidence class |
| `ProviderClaim` | Untrusted source observation (country, URL, external ID, metadata) | Any authority to execute | Explicitly **excluded** from trusted evidence |

**Default allowlist: empty.** Candidate classes are vocabulary for design review, not permission to activate any provider. No Jobicy, LinkedIn or Upwork source is approved for ingestion by this document. Unknown classes, issuers, scopes, permissions or freshness deny.

### Minimum issuance and revocation procedure (proposed)

1. A named governance owner approves the evidence class, verifier role, source/operation scope, validity window and independent verification method.
2. An authorized verifier checks evidence from an independently trusted source and records opaque reference, exact typed subject, issuer, procedure version, timestamp, expiry, evidence version and audit actor.
3. A separate policy determination confirms whether the intended access operation is permitted; identity or geography verification never substitutes for licensing/terms approval.
4. Changes in verified facts, terms, access status, source identity or expiry trigger authority-owned revocation/version bump. No provider-origin write can extend validity.
5. Gate denies on absent, stale, unknown, revoked or unprovable-current evidence; attempts already reserved obey the fenced revocation/start policy.
6. Reconciliation and governance reviews are auditable and least-privilege; uncertain external execution cannot be treated as non-start without authoritative proof.

### D0 approval checklist

- [ ] Appoint named accountable product, evidence-policy, authorization, reconciliation and audit/governance owners.
- [ ] Approve or reject each proposed evidence class and its independent verification procedure.
- [ ] Approve a source/operation-specific access-rights review; no general permission is inferred.
- [ ] Approve issuance/revocation actor separation, validity defaults and audit retention/access.
- [ ] Record explicit permission to implement contracts in a separately scoped issue/PR.

**Recommendation:** approve this as a *draft D0 ownership model for review*, not as an active authority registry. Do not change the empty allowlist until all D0 checks and a production-readiness gate have been completed.
