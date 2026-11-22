# ChatGPT ↔ Codex handoff protocol

> Scope: coordination through GitHub. This protocol **does not** automatically start or control a Codex Desktop session.

## Source of truth

- GitHub issue: goal, acceptance criteria, scope and human authorization.
- Draft PR: implementation, commit SHA, tests, CI evidence, reviews and blockers.
- `AGENTS.md`: repository engineering rules. This document only defines the handoff.
- Keep handoffs short; link to existing issue/PR rather than copying the whole history.

## Roles and boundaries

- **Product Owner (human):** approves scope changes, merge, deploy, external provider activation and costs.
- **ChatGPT (architect/orchestrator):** reads issue/PR and CI, publishes next task and review; cannot directly trigger a Codex Desktop session through GitHub comments.
- **Codex (engineer):** on session start, reads the active issue, latest task comment and `AGENTS.md`; implements within approved scope; reports evidence.

## Minimal handoff format

Post a PR comment with exactly one of these markers:

### `[COA:CODEX-TASK]`

```text
Issue: #<number>
PR: #<number>
Goal: <one sentence>
Scope: <specific files/behaviors>
Acceptance: <testable outcomes>
Constraints: <no merge/deploy/etc.>
```

### `[COA:CODEX-RESULT]`

```text
Commit: <SHA>
Changed: <brief summary>
Validation: <build/unit/SQL CI links and actual outcomes>
Remaining: <gaps/blockers>
Next: review requested
```

### `[COA:REVIEW]`

```text
Head: <SHA reviewed>
Decision: accepted | corrections required | blocked
Evidence: <diff/checks>
Next: <one concrete action>
```

### `[COA:BLOCKED]`

```text
Blocking decision: <what/why>
Options: <minimal alternatives>
Owner action: <specific approval or environment prerequisite>
```

## Working loop

1. Human starts Codex session when ready. Codex checks the latest `[COA:CODEX-TASK]` on the active Draft PR; if absent, request a task rather than guessing.
2. Codex makes scoped changes, runs narrow checks, then full required quality gates. Posts `[COA:CODEX-RESULT]`.
3. ChatGPT reviews latest commit and actual CI status, posts `[COA:REVIEW]` and the next `[COA:CODEX-TASK]` when authorized.
4. If scope or permissions change, post `[COA:BLOCKED]` and wait for Product Owner approval.
5. Never place tokens, credentials, private CVs or personal data in comments.

## Future automation

Event-driven execution requires a separately authorized runner, least-privilege credentials, audit trail, concurrency controls, spending limits and a kill switch. Do not configure this from the handoff protocol alone.
