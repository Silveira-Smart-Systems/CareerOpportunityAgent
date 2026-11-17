# COA — Model Routing Policy

Status: Proposed — candidate models require benchmark  
Tracking: #5; ADR-0004

## Capability-specific routing
Use separate typed ports for chat/reasoning, document parsing, embeddings, and reranking. Do not pretend embeddings/reranking are chat-completion endpoints.

| Task | Initial candidate | Fallback strategy |
|---|---|---|
| Fast classification and normalization | NVIDIA Nemotron 3.5 Bolt E4B | Alternative fast structured-output model after evaluation |
| Deep critical review and complex analysis | NVIDIA Nemotron 3.5 Super VL 120B A12B | Benchmark Nemotron 3.5 Lightning 30B A3B |
| CV/document parsing | NVIDIA Nemotron 3.5 Parse | Local text extraction where format permits; flag quality differences |
| Semantic embeddings | NVIDIA Nemotron 3.5 Embed 1B | Explicitly version and re-index if embedding model changes |
| Candidate reranking | NVIDIA Nemotron 3.5 Rerank 8B | Deterministic ranking when unavailable; measure impact |

These names are **configuration candidates**, not verified API availability, quality, or production commitments. Actual provider model IDs and API contracts must be verified during implementation.

## Routing contract
Route by capability, task complexity, policy version, latency/cost budget, provider health, and data-handling constraints. The routing decision is auditable; fallback cannot silently change prompt policy or score semantics.

## Operational safeguards
- Configure per-task timeouts, token/input limits, retry budgets, and concurrency ceilings.
- Use circuit breakers or equivalent controls only where provider failure rates justify them.
- Track estimated cost, model errors, schema validation failures, and quality by task.
- Keep API keys server-side; redact PII and sensitive content where feasible.
- If an essential capability fails, persist partial/failed status rather than inventing results.
- Cache only when privacy, source-version, and policy-version boundaries are satisfied.

## Benchmark plan
Build a labeled dataset of at least 30 real opportunities (redacted for repository fixtures). Evaluate extraction correctness, evidence fidelity, precision@5, unsupported claims, latency, cost/opportunity, and hard-constraint violations. Compare candidates before setting production defaults.

## Future SaaS considerations
Per-user budgets, usage quotas, model-provider data processing agreements, isolation, and cost allocation are deferred until multi-user validation, but interfaces should not hard-code User Zero identity.