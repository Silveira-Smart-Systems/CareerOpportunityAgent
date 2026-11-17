# Deterministic Scoring Policy — first increment

Tracking: #16, ADR-0005. Status: Draft for review.

## Scope
- Adds `ScoringPolicy` with explicit version `mvp-0.1`.
- Provisional weights: technical 40, commercial 25, work-model 15, seniority 10, career-alignment 10.
- Missing evidence dimensions are **UnknownNotProven**, never assumed matched.
- Hard constraints block eligibility independently of numerical score.
- Unknown dimensions and invalid policy weights are rejected.
- No LLM inference or provider API calls are involved.

## Limitations
- Current API still accepts caller-supplied weights via `MatchEvaluator`. It is a synthetic-data bootstrap endpoint and must not be used as authoritative production ranking.
- Persistence, scoring snapshots, authorization, and source-linked evidence are future increments.
- A high numerical score with `Eligible = false` is intentional; UI/reporting must not present it as an eligible recommendation.
- This initial policy is a product hypothesis requiring evaluation against labeled opportunities.
- Tests must pass in CI before human-approved merge.
