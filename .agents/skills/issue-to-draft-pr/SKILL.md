---
name: issue-to-draft-pr
description: Implement one approved COA GitHub issue as a minimal tested change and Draft PR; use for bounded development work with acceptance criteria.
---

1. Read the issue, acceptance criteria, linked ADRs, and only the affected files. Ask for scope if ambiguous.
2. Summarize a short implementation plan and risks; do not expand scope.
3. Create a branch from current main. Make focused changes and relevant tests/docs.
4. Validate narrowly first; then run `dotnet build COA.slnx --configuration Release` and `dotnet test COA.slnx --configuration Release` when available.
5. Create a Draft PR linked to the issue. Include scope, tests actually run, risks, and pending decisions.
6. Stop. Human must approve merge/deploy; do not close the issue until acceptance criteria are verified.
