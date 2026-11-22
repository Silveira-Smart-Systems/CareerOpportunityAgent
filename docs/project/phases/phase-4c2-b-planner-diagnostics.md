# Phase 4C.2-B — Planner & Diagnostics implementation checkpoint

Parent: [#67](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67). Predecessor: [PR #69](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/69) (merged).

## Design constraints
- Preserve the existing `SearchRequest.SourceMarkets`, `IDiscoveryPlanner.CreatePlan`, `DiscoveryPlan` and legacy execution behavior. Introduce a separate, explicitly invoked scoped planning path; no implicit legacy-market-to-publication-country conversion.
- Use `DiscoveryGeographyIntent.PublicationMarket` to select **publication** scopes only: `KnownCountries` maps to explicit `PublicationCountry` scopes; `Unconstrained` maps only to explicit `UnconstrainedPublication`. Never infer either from advertised geography or remote eligibility.
- A source is eligible for a scoped target only when the corresponding scope capability has been explicitly registered after external evidence verification **and** mandatory filters, authorization, licensing, health, quota, budget and restrictions pass. `Supports()` alone is not an authorization decision.
- Return deterministic, source- and scope-specific diagnostics for unsupported scope, unknown/unsupported mandatory criteria, authorization, license, health, quota, budget and temporary restrictions. Stable ordering by source ID and scope kind/country; do not widen searches on failure.
- Never execute a provider from this increment. Planner results are not permits. Scoped gate/orchestrator changes are deferred to 4C.2-C.
- Do not enable Jobicy, perform HTTP requests, or implement issue #47 ingestion, persistence, scheduling or global deduplication.

## Proposed implementation sequence
1. Add typed scoped planning request/snapshot/result contracts with strict validation and immutable inputs; keep existing contracts intact.
2. Add explicit scope selection and deterministic diagnostics in a new scoped planner API; no runtime wiring.
3. Add unit tests for PT vs legacy PT distinction, unconstrained vs known publication, unsupported-by-default, missing/invalid evidence reference, mandatory fail-closed, operational denials, ordering and mutation safety.
4. Validate via CI, inspect diff and request review. Keep this PR Draft until explicit owner approval.

## Risks and safeguards
- **Evidence trust boundary:** evidence reference is not verified by the contract; registration must verify real provider evidence before declaring support.
- **Accidental widening:** do not infer unconstrained scope from a list of countries or a legacy market.
- **Backward compatibility:** additive API and regression tests for legacy planner.
- **Execution race/TOCTOU:** 4C.2-B diagnostics do not grant execution; 4C.2-C must revalidate atomically at the gate.

## Checkpoint
This document records the design and implementation order. Code, tests and CI verification are tracked in this Draft PR before any merge.
