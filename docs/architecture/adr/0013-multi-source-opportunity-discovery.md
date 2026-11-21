# ADR-0013: Capability-based multi-source opportunity discovery

- **Status:** Accepted
- **Date:** 2026-11-21
- **Issue:** #53
- **Related:** ADR-0007, #45, #46, #47, #53, #54

## Context

COA must discover opportunities from zero, one, or several authorized sources according to a user's search criteria, source coverage, source capabilities, rights, and operational conditions. A source's supported countries, filter semantics, data completeness, licensing, rate limits, and availability differ. In particular, the evaluated Adzuna API supports GB and BR but not PT; an unsupported market is not equivalent to zero results.

The existing manual opportunity import is owner-scoped and must continue to work. It is not a global opportunity catalogue and must not be reinterpreted as one without a separate ingestion and persistence decision.

## Decision

Keep Domain and Application provider-neutral. They must not reference provider SDKs, HTTP contracts, or external DTOs. A future search profile is translated to a neutral `SearchRequest`; Foundation tests may construct that request directly and do not require a persisted `JobSearchProfile`.

Separate source information into three concepts:

- `SourceCapabilities`: stable provider characteristics: supported source markets, filter operations and semantics, pagination, and field completeness/quality.
- `SourceExecutionPolicy`: controlled configuration: authorization, licensing and permitted data use, retention/redistribution rules, rate limits, cost, and budget.
- `SourceRuntimeStatus`: dynamic operational observation: health, current quota availability, and temporary restrictions.

`ISourceCapabilityRegistry` exposes capabilities. A planning snapshot combines the three concepts immutably for a single decision. `IDiscoveryPlanner` receives a neutral request and snapshot and produces a deterministic `DiscoveryPlan`, including an inclusion/exclusion decision and reasons for every source and criterion.

The planner may select zero, one, or several sources. A mandatory criterion must be supported by the provider or be safely post-filterable from complete, permitted data. It must never be omitted or widened silently. Unsupported preferences may be retained only with an explicit diagnostic.

`IDiscoveryOrchestrator` is a later concern. It executes a plan with bounded concurrency and timeouts, revalidates policy and runtime status immediately before calls, and isolates partial failures. It is not part of Foundation.

Adapters are Infrastructure implementations. Each encapsulates authentication and external API details, translates planned neutral criteria to provider parameters, and returns a common intermediate `SourceOpportunityCandidate`. It preserves source identifiers, URLs, provenance, observed values, unknown attributes, and structured failures. It does not perform global normalization, deduplication, persistence, or matching.

Discovery and ingestion remain separate. Discovery selects and searches sources. A later ingestion pipeline validates data-use rights, normalizes candidates, preserves source occurrences/provenance, deduplicates, and persists only when authorized. Matching remains a separate evaluation of a search profile against a normalized global opportunity.

## Consequences

- PT-only searches exclude Adzuna before any call. GB-only searches may select Adzuna when policy and runtime state permit it. PT plus explicitly selected international markets can fan out only to eligible market/source pairs.
- If no source is eligible, the system returns an empty plan/outcome with diagnostics instead of attempting an external call.
- A hypothetical Portugal-capable provider can be represented in tests without asserting that an authorized real provider exists.
- The current manual import and deterministic matching flow remain unchanged until a separately approved catalogue/ingestion migration.
- Foundation (#54) introduces only neutral contracts, models, the registry/snapshot, planner, and fake-driven tests. It introduces no adapters, network calls, public endpoints, database changes, migrations, or dependencies.
- Provider integration requires the authorization and data-rights gate in #46. Global catalogue, provenance persistence, and deduplication remain work for #47.

## Alternatives

- **Provider-specific logic in Core:** rejected because source coverage and semantics would leak into search, matching, and product policy.
- **One source selected globally:** rejected because PT and international coverage require independent capability-based selection.
- **Combine capabilities, policy, and runtime status:** rejected because stable facts, configured rights, and transient state change at different rates and have distinct owners.
- **Normalize and deduplicate inside adapters:** rejected because cross-source consistency and provenance require a separate ingestion concern.
- **Silently drop unsupported mandatory filters:** rejected because it changes the user's requested search and creates misleading results.

## Revisit when

Revisit when a source is approved under #46, when #47 defines the global catalogue and source-occurrence persistence model, or when the first execution policy exposes a missing responsibility.
