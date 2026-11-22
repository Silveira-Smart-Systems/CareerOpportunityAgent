# COA agent guidance

- Repository source of truth: GitHub issues, PRs, versioned docs and decisions. Never rely on chat memory alone.
- Follow `CONTRIBUTING.md` and `docs/engineering/quality-gates.md`. Read only relevant sections, not the entire docs tree.
- Stack: .NET 10, Blazor Web App (Interactive Server for authenticated screens), Auth0 server-side OIDC, separate API. See `docs/architecture/adr/0011-blazor-web-app-render-modes.md` when relevant.
- Start from a GitHub issue with acceptance criteria. Inspect targeted files first; propose a small plan, then change only the required scope.
- Prefer simple, maintainable code and existing patterns; no speculative abstractions, extra packages, or unrelated refactors.
- Run the narrowest relevant validation first, then Release build and tests before proposing merge. Report actual results and skips.
- Never commit secrets, real CVs, tokens, or personal data. Do not expose Auth0 credentials in prompts or logs.
- Open Draft PR with issue link, validation evidence and risks. Human approval required for merge, deploy, publishing and job applications.
- Keep output concise: outcome, files changed, tests, blockers, next human decision. Stop when acceptance criteria are met.
- For reusable procedures, use on-demand skills in `.agents/skills/`; avoid loading every skill or spawning agents for simple work.
- For ChatGPT ↔ Codex coordination, read `docs/engineering/agent-handoff-protocol.md` only when receiving or reporting a GitHub-mediated task.
