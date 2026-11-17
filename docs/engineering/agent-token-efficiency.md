# Token-efficient agent customization for COA

Status: Proposed — validate locally in Codex Desktop and GitHub Copilot before treating as enforced behavior. Research date: 2026-11-17.

## Evidence and decisions

Official Codex guidance recommends a short repository `AGENTS.md` for durable rules and on-demand skills in `.agents/skills/<name>/SKILL.md`; skill metadata is discoverable before full instructions are loaded (progressive disclosure). GitHub distinguishes always-on instructions, prompt files, skills, custom agents and subagents. GitHub recommends specific tasks, relevant context, explicit stopping criteria, fresh sessions for unrelated work and a limited MCP toolset.

References:
- https://learn.chatgpt.com/docs/customization/overview
- https://learn.chatgpt.com/docs/build-skills
- https://docs.github.com/en/copilot/reference/customization-cheat-sheet
- https://docs.github.com/en/copilot/tutorials/optimize-ai-usage
- https://docs.github.com/en/copilot/concepts/prompting-and-context/custom-instructions

## COA implementation strategy

| Layer | Location | Purpose | Loading |
|---|---|---|---|
| Core rules | `AGENTS.md` | Short non-negotiable guardrails and repo map | Always |
| Skills | `.agents/skills/*/SKILL.md` | Repeatable detailed procedures | On demand |
| Commands / prompts | `.github/prompts/*.prompt.md` | Reusable manual task entry points in supported Copilot clients | Explicit; not assumed supported by Codex Desktop |
| Specialized agents | Later, after benchmark | Distinct permissions and isolated responsibilities | Only when complexity warrants |

Avoid duplicating `AGENTS.md` verbatim in `.github/copilot-instructions.md` or each agent prompt. Check actual client discovery behavior before adding overlapping instructions. Do not assume the same command syntax or auto-discovery in Codex and Copilot.

## Operational token budget rules

1. **Scope first:** provide issue, goal, acceptance criteria, likely paths and stopping condition.
2. **Read selectively:** search file names/symbols before opening files; avoid recursive dumps, whole solutions and full CI logs.
3. **Use progressive disclosure:** keep skill descriptions specific; put large examples and references in separate files only when needed.
4. **Minimize orchestration:** one agent for simple tasks; delegate only independent, complex subtasks with bounded deliverables. Subagents incur extra work/context.
5. **Keep toolsets lean:** activate only relevant MCP servers/tools. No external provider credentials in context.
6. **Bound retries:** after a failed build, inspect the first root error; do not rerun unchanged commands repeatedly.
7. **Fresh sessions for new issues:** persist decisions in GitHub, then start a new context. Compact long sessions only when continuing the same issue and supported by client.
8. **Use deterministic checks:** prefer compiler/tests/lint over asking an LLM to reread every file.
9. **Stop explicitly:** no speculative features, autonomous merge, or unnecessary agent reviews after criteria are satisfied.

## Baseline experiment (before expanding)

Run the same two small, non-sensitive COA issues with (A) a general agent with the short AGENTS.md and (B) the relevant skill. Record model, client, task, files read, tool calls, retries, elapsed time, build/test results, correctness and token usage **if the client exposes it**. Do not invent token savings or compare different models without noting confounders. Record findings in a GitHub issue and update guidance based on measured results.

## Adoption gate

Start with the two skills in this PR. Add a manual prompt file only when a repeated command is identified and tested in the target client. Add a specialized agent only when a recurring task requires a distinct tool permission boundary or isolated context. Do not build a large agent catalogue preemptively.

## Security and approval

Follow CONTRIBUTING.md: no secrets or real personal data in prompts, logs or commits. All code changes via issue/branch/Draft PR, CI and human review. No automatic merges or deployment.
