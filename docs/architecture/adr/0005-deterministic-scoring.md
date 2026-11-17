# ADR-0005: Deterministic final scoring with evidence-backed AI analysis

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
LLM-generated numerical rankings can drift and are difficult to audit. Product trust depends on explaining fit and hard-constraint violations.

## Decision
Use models to extract and analyze source-linked evidence; compute final weighted scores in deterministic code using versioned policies. Distinguish Matched, Missing, and Unknown/NotProven. Treat Technical 40%, Commercial 25%, Work Model 15%, Seniority 10%, Career Alignment 10% as provisional weights. Evaluate hard constraints separately from score.

## Alternatives considered
End-to-end LLM scoring; keyword-only ranking; embedding similarity as final score.

## Consequences and trade-offs
Reproducible scores for fixed snapshots and policy versions; requires explicit feature definitions and labeled evaluations. Critical review can flag issues but cannot silently override policy.

## Revisit when
Evaluation on real labeled opportunities demonstrates that another calibrated method yields better reliability without sacrificing explainability.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
