# Engineering Quality Gates

Status: Proposed  
Tracking: #6

## Required before merging implementation PRs
1. Restore and build solution in Release mode with warnings reviewed.
2. Run unit tests; add integration tests for SQL/persistence, architecture tests for module dependency rules, and agent evaluation tests for model/prompt changes.
3. Validate relevant FR/AC/NFR references from `docs/product/requirements.md`.
4. Confirm idempotency, failure behavior, and structured logs for background processing.
5. Confirm authorization, data minimization, and untrusted-content handling for external inputs.
6. Verify no secrets/private CVs in committed files.
7. Human approval; CI passing is necessary but not sufficient.

## Test strategy
| Suite | Focus | CI execution |
|---|---|---|
| Unit | Scoring, constraints, normalization, invariants | Every PR after solution exists |
| Integration | SQL Server repositories, transaction boundaries, adapters | Every PR when provisioned; otherwise explicit skipped status |
| Architecture | Module dependencies, forbidden references | Every PR after solution exists |
| AgentEvaluation | Golden fixtures, prompt injection, evidence fidelity, regressions | Every agent/prompt/model change; stable offline fixtures in PR CI |

## CI rollout
This repository initially contains documentation only. The bootstrap workflow checks whitespace/conflict markers, repository hygiene, and documentation paths. When a `.sln` or `.slnx` is committed, the workflow also restores, builds, and runs available tests. Do not interpret an absent solution as a successful application build.

## Future CI enhancements
- Dependabot/dependency vulnerability checks
- Format/lint checks with pinned tool versions
- Coverage thresholds based on meaningful tests, not vanity numbers
- Integration test SQL Server service once schema exists
- Prompt/model benchmark jobs with secure secrets and explicit cost ceilings (not on untrusted PRs)
- Artifact provenance and deployment gates

## Branch protection
After CI is stable, require checks and human review in repository settings. Merge/deployment remain human-approved.
