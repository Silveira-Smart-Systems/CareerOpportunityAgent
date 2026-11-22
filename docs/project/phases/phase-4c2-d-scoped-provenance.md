# Phase 4C.2-D — Scoped Provenance Evolution (design checkpoint)

Tracking issue: [#72](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/72). Parent: #67. Prerequisite: merged PR #71.

## Problem

The scoped executor preserves the requested `SourceExecutionScope`, but a requested scope is **not** evidence that a job was actually published in that geography. Legacy `RequestedMarket` cannot represent `UnconstrainedPublication` and must not be synthesized. Source URL and external ID identify an origin claim, not verified licensing or eligibility evidence.

## Proposed boundaries

1. **Execution intent**: exact `ScopedSourceExecutionTarget` and criteria sent to the adapter; preserve the three disjoint scope variants (`LegacyMarket`, `PublicationCountry`, `UnconstrainedPublication`).
2. **Observed source claim**: geography claimed by the provider, potentially absent, ambiguous or multi-country; never infer from the execution request.
3. **Origin identity**: provider source ID, nonblank external ID, absolute HTTP(S) canonical source URL; separate from optional stable deduplication keys. URLs are not proof of publication country or uniqueness.
4. **Evidence metadata**: source attribution and retrieval time, with an explicit verified/unverified/unknown distinction. Evidence verification belongs to a trusted authority, not an arbitrary string field.
5. **Trace metadata**: correlation and operation identifiers where available, excluding credentials, sensitive query values, and unmasked tokens.

## Validation requirements

- Reject mismatched source IDs or requested scopes, blank IDs, malformed/non-HTTP(S) URLs, null candidate fields, and duplicate/conflicting mandatory criteria.
- Preserve unknown geography as unknown; distinguish unknown from unconstrained request intent.
- Never promote provider claims to verified policy/licensing evidence.
- Define identity stability and deduplication independently of requested scope; do not merge unrelated opportunities solely by URL.
- Use additive contracts/mapping so the legacy orchestrator and consumers continue to compile; no silent field repurposing.
- Fail closed where provenance is mandatory and unverifiable, but keep diagnostics explicit.

## Test matrix

| Scenario | Expected behavior |
| --- | --- |
| PublicationCountry(PT) requested; source claims ES | Retain PT request and ES claim separately; flag mismatch according to documented policy |
| UnconstrainedPublication requested; source gives no geography | Preserve unconstrained request and unknown observed geography |
| LegacyMarket(PT) requested | Preserve legacy identity without rewriting it to publication country |
| Wrong source/scope in result | Reject result |
| Empty external ID or non-HTTP(S) URL | Reject result |
| Repeated URL for distinct external IDs | Do not silently deduplicate |
| Provider returns unverified evidence string | Never treat as verified authorization |
| Cancellation or timeout | Preserve permit accounting and no fabricated provenance |

## Out of scope / production boundary

No provider activation, scheduler, persistence, ingestion, or issue #47. Jobicy remains disabled. The in-memory reservation authority remains a **single-process test/reference implementation**, not a distributed transactional production authority.

## Review gates

1. Review the semantics and migration plan in Draft PR.
2. Implement additive contracts and validation only after design approval.
3. Run unit tests, CI, and compatibility checks.
4. Require explicit human approval before merge.

## Implemented conflict policy (Draft PR #73)

- `ScopedGeographyClaimEvaluator` compares observed country claims only when the requested scope is `PublicationCountry`. A matching country in a multi-country claim is a match; a claim containing no requested country is a conflict.
- A missing or explicitly unknown claim yields `Unknown`, never a fabricated country. `LegacyMarket` and `UnconstrainedPublication` with observed country claims yield `NotComparable`; neither is implicitly converted into a publication-country request.
- For a conflicting claim, the scoped orchestrator preserves the candidate and its original requested scope and observed claim, returns `Succeeded` for an otherwise valid adapter result, and emits one `ObservedGeographyConflict` diagnostic per conflicting candidate, with the optional `ExternalId`. No silent correction, filtering, authorization, or ingestion follows from the diagnostic.
- Invalid mandatory provenance (wrong source/scope, blank external ID, unsupported URL, missing UTC retrieval time) remains rejected independently of geography claim assessment. A provider claim is not independently verified geographic evidence.
- Existing five-argument `ScopedSourceProvenance` construction and two-argument `ScopedSourceExecutionDiagnostic` construction remain supported. No legacy orchestrator behavior is changed.

## Review status and remaining boundaries

The scope/provenance contracts, geography evaluator, scoped outcome diagnostics and regression tests are implemented in the Draft PR. The implementation deliberately does not introduce independent evidence verification, field-level evidence or trace metadata, canonical identity/deduplication, or production-grade distributed authorization. These remain explicit future design concerns, not features inferred from the presence of provenance fields. The PR must remain Draft until review and explicit merge approval.
