# ADR-0004: Provider-neutral AI capabilities and model routing

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
NVIDIA NIM is a candidate provider, but model availability, quotas, pricing, latency, and accuracy may change. Chat, parsing, embeddings, and reranking are distinct capabilities.

## Decision
Define typed ports per capability and a model router driven by task, policy, budget, and availability. Keep prompts versioned; validate structured outputs and record model/prompt versions, latency, tokens and costs where available. Benchmark NVIDIA models before production use.

## Alternatives considered
Hard-code one model/SDK; treat every capability as chat completion; adopt a large agent framework upfront.

## Consequences and trade-offs
Provider substitution and controlled fallbacks are easier, at the cost of adapters and evaluation effort. No automatic model fallback may silently alter output quality or policy without observability.

## Revisit when
A proven single provider or standardized gateway makes a simpler integration demonstrably safer and cheaper.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
