# Contributing to Career Opportunity Agent

## Single source of truth
All project tasks, decisions, documentation, architecture changes, and deliverables must be recorded or linked in this GitHub repository. Do not rely on chat history or agent memory as the only record.

## Workflow
1. Create or reference an Issue with scope and measurable acceptance criteria.
2. Branch from current `main`: `feat/<issue>-<topic>`, `fix/<issue>-<topic>`, `docs/<issue>-<topic>`, or `chore/<issue>-<topic>`.
3. Make small, traceable commits. Include issue references.
4. Open a **Draft PR** early; explain purpose, tests, risks, and outstanding decisions.
5. Run CI, request human review, and resolve feedback.
6. Human owner approves the merge. Prefer squash merge for a focused Issue.
7. Close an Issue only after its acceptance criteria are satisfied and verified.

## Approval boundaries
Agents may create issues, branches, commits, drafts, and review suggestions when authorized. **No agent merges, deploys, publishes, submits job applications, or changes production credentials without explicit human approval.**

## Quality gates
- No secrets, private CVs, private applications, or unauthorized vacancy redistribution in the repository.
- CI checks repository hygiene and, once a .NET solution exists, restore/build/tests.
- Changes to domain rules, scoring weights, prompts, model routing, and external integrations require tests/evaluation and versioned documentation.
- PRs must include acceptance criteria and evidence of validation; unknowns and skipped tests must be explicit.
- Architecture boundaries: domain does not reference infrastructure; agents cannot directly own persistence; no outbound application submission in MVP.
- Never bypass failing checks silently; document any temporary exception in an Issue.

## Branch protection (manual repository setting)
Enable required status checks, PR review, and prevent direct pushes to `main` once checks are stable. GitHub settings must be configured by an authorized repository administrator; this file does not change them.

## Commit style
Prefer `type(scope): concise description (#issue)` (e.g. `docs(agents): define prompt policy (#5)`).

## Privacy
Use synthetic or irreversibly redacted fixtures. Never put API keys in source or sample files. Use environment variables / .NET User Secrets locally and an appropriate secret store in deployment.
