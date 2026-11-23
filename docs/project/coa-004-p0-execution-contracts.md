# COA-004 — P0 execution contracts (proposal)

Status: **Proposed — Product Owner acceptance checkpoint**  
Date: 2026-11-23  
Baseline: `main` after merged PR #91.  
Sources: #48, #49, #77, #89, #90, #94, `AGENTS.md`, `docs/engineering/agent-handoff-protocol.md`.

## Shared invariants

- Existing .NET 10 / Blazor Interactive Server / API / SQL stack; no React migration.
- Owner identity scopes profile, preferences, opportunities and computed assessments. No cross-owner evidence or cache leakage.
- Synthetic opportunities remain explicitly labelled; no real provider ingestion or invented facts.
- Preserve existing public HTTP JSON contract for `GET /api/opportunities` and `GET /api/opportunities/{id}`; schema changes require a separate contract review.
- No merge, deployment, provider activation, new external services, spending, or autonomous runner authorization.
- Separate branches and Draft PRs for B1 and F1. Independent reviewer/test evidence and passing required CI before a human merge decision.

## B1 — #48: Matching evidence correctness

**Files of interest:** `src/COA.Application/Opportunity/OpportunityUseCases.cs`, existing matching domain, scoped tests. No speculative new scoring engine.

1. With neither career profile nor active search preferences, vacancy text alone **must not** prove a user-specific technical or work-model match.
2. `UnknownNotProven` is distinct from a proven mismatch (`Missing`); unknown evidence scores zero under the current policy and must not be described as negative proof.
3. Work-mode compatibility must be grounded in both a user's explicit preference and the vacancy's supported evidence. Unknown work arrangement is never silently remote. Distinguish remote, hybrid and onsite when explicitly evidenced; do not implement speculative geospatial routing.
4. **D1 accepted by PO (2026-11-23):** The user-facing work-mode choice includes **Remote / Hybrid / Onsite / No preference** (Portuguese: Remoto / Híbrido / Presencial / Sem preferência). **No preference** means no work-mode eligibility restriction; it does **not** assert that the opportunity's work mode is known or matched. Model it as absence of a work-mode restriction, not necessarily a fourth `SearchWorkMode` enum value. When the user explicitly selects permitted modes, eligibility requires proven compatibility; unknown job work mode remains `UnknownNotProven` and does not satisfy a declared hard constraint. Preserve existing scoring weights and distinguish score from eligibility. Inspect current multi-select preferences and preserve support for multiple permitted modes (e.g. Remote + Hybrid), rather than inadvertently restricting the domain to a single mode.
5. Preserve deterministic, owner-scoped assessments; two users may receive different assessments for the same opportunity. No user-independent default scoring.
6. Do not implement B2 persisted provenance, freshness, recalculation infrastructure or public schema evolution in B1.

**Acceptance tests:** no-profile/no-active-search returns no inferred matched evidence; remote text without a user remote preference is not proof of compatibility; unknown mode remains unknown; explicitly incompatible mode cannot become a match; two-owner isolation and deterministic score; unchanged API serialization and current domain evaluator invariants.

## F1 — #49: Existing Blazor UX improvement

**Files of interest:** `src/COA.Web/Components/Pages/Opportunities.razor`, `OpportunityDetail.razor`, `Dashboard.razor`, scoped styles and Web tests.

1. Keep existing API client and endpoint payloads. Consume current `MatchAssessment` with `Score`, `Eligible`, and `Criteria`; do not invent new explanation fields.
2. Visually distinguish numeric score, hard-constraint eligibility and unknown/missing/matched evidence. Do not imply that a high score guarantees eligibility.
3. Accessible and responsive list/detail with keyboard/focus semantics, readable labels and contrast; preserve existing import and synthetic/demo markers.
4. Explicit loading, empty, failure/retry, and access-denied states where supported; do not infer permission-denied from a generic network error.
5. Existing Draft PR #78 and design-only #77 must be reviewed for overlap. F1 code is bounded by the explicit COA-004 PO authorization and this P0 checkpoint, not by a blanket approval of all UX features.
6. No unapproved filters, save/apply tracking, real jobs, API changes or frontend migration.

**Acceptance tests:** Web contract/build tests, accessibility/responsive manual checklist with screenshots, score/eligibility/unknown differentiation, synthetic label, empty/loading/error/retry and owner-denied behavior without leaking details.

## Execution DAG and review gates

```text
P0 proposed contract + PO/architect acceptance
  ├── B1 (#48): backend matching + unit/security regression tests -> Draft PR B1
  ├── F1 (#49): Blazor UI + Web tests -> Draft PR F1
  └── O1 (#94): orchestration feasibility + proposed ADR only
B1 + F1 -> independent reviews -> CI -> human merge decision (not preapproved)
```

B1 and F1 may run in parallel only after P0 acceptance. B1 owns matching application/domain/tests; F1 owns Web pages/styles/Web tests. Any shared DTO/API contract change blocks the affected task until reviewed. No agent self-approves.

## Orchestration feasibility (#94)

Existing GitHub-mediated handoff uses `[COA:CODEX-TASK]`, `[COA:CODEX-RESULT]`, `[COA:REVIEW]`, `[COA:BLOCKED]`. Comments do **not** launch Codex Desktop sessions. O1 must evaluate supported execution surfaces and propose an ADR for trust boundaries, approval, least privilege, state machine, task IDs, idempotency, retries, concurrency, token/cost limits, audit, independent review and kill switch. No runner activation.

## Open PO acceptance decisions

- **D1 — ACCEPTED (2026-11-23):** Explicit UI option **Sem preferência**; no work-mode eligibility restriction, with unknown vacancy evidence still `UnknownNotProven`; retain existing multi-mode capability.
- **D2:** Accept B1/F1 bounded scopes and unchanged public API contract as written?
- **D3:** Confirm F1 may proceed with incremental implementation before the full Figma high-fidelity approval, using existing approved UX baseline, while #77 high-fidelity design remains separate?

No B1/F1 runtime code changes until these decisions are accepted and documented.
