# Codex Desktop onboarding and agent orchestration readiness

Status: Proposed — requires owner review. Date: 2026-11-17.

## Goal
Start with one supervised Codex development workflow on Career Opportunity Agent (COA). Introduce specialized agents only after issue-to-PR delivery is repeatable. The GitHub repository remains the source of truth for requirements, tasks, decisions, tests and deliverables; chat and agent memory are not authoritative records.

## Owner checklist for the next working session
1. Open the cloned repository in Codex Desktop (or clone it if not yet present), on the latest `main`; check `git status` and pull before creating a branch.
2. Confirm .NET 10 SDK and run `dotnet restore COA.slnx`, `dotnet build COA.slnx --configuration Release`, and `dotnet test COA.slnx --configuration Release`. Report failures rather than bypassing them.
3. Keep Auth0 domain/client ID/client secret only in local User Secrets. Never paste secrets, tokens, or personal profile data into issues, logs, prompts, or PRs.
4. Verify the PR #27 logout behavior in the local browser: sign in, sign out, then sign in again. Note that a federated upstream IdP session can remain even after Auth0 logout. Record actual observed behavior, not assumptions.
5. Confirm repository Settings → Rules / Branch protection require successful CI and human PR review for `main` (administrator action; not enforced by this document).
6. Choose one small issue for the first Codex-assisted delivery; agree acceptance criteria before implementation.

## First supervised workflow
- **Intake:** An issue states purpose, scope, out-of-scope, acceptance criteria, relevant ADRs, tests, and privacy risks.
- **Plan:** Agent inspects current code and relevant docs, identifies dependencies and proposes minimal change. Human approves scope for sensitive work.
- **Implement:** One branch per issue; narrow changes, no unrequested refactoring; update tests and documentation alongside behavior.
- **Validate:** Run focused tests and full CI. Report commands and results honestly; do not claim local validation if only remote CI ran.
- **Review:** Open Draft PR linked to issue, with evidence, risks and open questions. Human owner approves merge.
- **Close:** Update issue and project docs in GitHub; no untracked decisions retained only in chat.

## Progressive agent adoption
**Stage 1 — one supervised coding agent:** Codex works on one issue at a time. Human owns prioritization, architecture and merge. Gate: at least two consecutive small PRs with passing CI, relevant tests and human review, without manual recovery from undocumented agent actions.

**Stage 2 — bounded specialists:** Introduce planning/issue triage, architecture review, implementation, testing and code review roles. Assign each a defined output artifact and permissions. Keep orchestration simple: coordination may produce assignments rather than assuming the platform supports native agent-to-agent delegation.

**Stage 3 — orchestrated execution:** Orchestrator reads GitHub issues and PRs, coordinates dependencies and reports evidence-backed status. It may create Draft PRs and suggest changes when authorized, but cannot merge, deploy, publish, submit applications, modify production credentials or bypass checks. Human owner remains the decision-maker. Ongoing monitoring requires an explicitly configured automation or scheduled workflow; a chat session alone is not continuous monitoring.

## Proposed next engineering slices (confirm against existing issues before creating duplicates)
1. Verify and harden Auth0 session handling: manual logout check, identity claim mapping (`iss`, `sub`, `name`, `email`), and tests for unauthorized access; never log raw identity tokens.
2. Implement authenticated owner-scoped career profile persistence following issue #20 and existing architecture decisions; enforce ownership in Application/domain boundary, not only UI.
3. Connect the Blazor profile screen to the approved Application use case, with validation and tests.
4. Then evaluate opportunity ingestion and matching against existing issue backlog.

## Required PR report
- Issue and acceptance criteria
- Files changed and design rationale
- Tests run, exact commands and results; skipped tests identified
- Security/privacy implications
- Documentation/ADR updates
- Remaining risks, dependencies, and human decisions

## Non-goals for now
No autonomous merge/deploy, no agent swarm, no paid infrastructure, no automatic job applications, no production credentials, no premature Interactive Auto/WASM migration.

## References
- `CONTRIBUTING.md`
- `docs/engineering/quality-gates.md`
- `docs/architecture/adr/0011-blazor-web-app-render-modes.md`
- GitHub issues #20, #23 and PR #27
