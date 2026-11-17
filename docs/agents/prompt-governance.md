# COA — Prompt Governance and Security

Status: Proposed — human review required  
Tracking: #5

## Prompt lifecycle
Prompts are versioned repository assets, with a manifest specifying agent name, task, input/output schema version, model capability, safety policy, and evaluation fixtures. Any production change must be traceable to an Issue and reviewed PR.

## Prompt template structure
1. Trusted role and objective.
2. Explicit allowed actions and forbidden actions.
3. Typed input schema and data provenance rules.
4. Untrusted source content, clearly delimited.
5. Output schema with mandatory uncertainty/evidence fields.
6. Error/abstention behavior when evidence is insufficient.
7. Version identifier and evaluation reference.

## Untrusted-content boundary
Vacancy text, recruiter messages, CVs, uploaded files, URLs, and model-generated summaries are untrusted data. Instructions embedded within them must not change system policies, call tools, request secrets, or trigger outbound actions.

## Defense in depth
- Restrict agent tools/capabilities by task; no general shell or unrestricted network tools in runtime agents.
- Validate all model outputs against schemas and domain rules.
- Enforce hard constraints and human approval in deterministic application code.
- Minimize external provider inputs and protect PII/secrets.
- Never treat a model's self-reported confidence as evidence.
- Avoid persisting full raw prompts containing private data unless an explicit privacy design permits it.

## Evaluation and promotion
- Maintain positive and adversarial fixtures for each agent.
- Compare candidate prompt/model versions against baseline before promotion.
- Record failures, regressions, quality/cost/latency metrics, and human sign-off.
- Roll back via versioned prompt and routing policies if quality degrades.
- Historical assessments retain their original prompt/model/policy versions.

## Repository convention (proposal)
```text
prompts/
  manifest.yaml
  profile-intelligence/
  matching/
  commercial-analysis/
  critical-review/
  application-assistant/
tests/
  COA.AgentEvaluationTests/
```
No executable prompts are committed by this document; implementation follows review of contracts and evaluation policy.

## Privacy rule for public repositories
Never commit personal CVs, private applications, API tokens, or raw vacancy content whose redistribution is not permitted. Use synthetic or properly redacted fixtures.