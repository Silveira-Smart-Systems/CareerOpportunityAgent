# Discovery MVP — delivery plan

Status: proposed for review · 2026-11-19
Source of truth: [Product Vision #45](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/45)

## Outcome
A signed-in user with a profile opens the dashboard and sees **real opportunities automatically collected from a permitted source**, ranked by an explainable profile-specific match, and can open the original posting.

## In scope
1. **Source feasibility gate**: document permitted access, licensing/ToS, rate limits, retention and display rights, auth and costs for candidate sources. Select **one authorized source** before implementation; do not assume LinkedIn/Upwork scraping is allowed.
2. **Ingestion vertical slice**: connector contract, scheduled ingestion, normalization, canonical URLs, source identifiers, deduplication, provenance, observability and failure/retry handling.
3. **Global catalog + user-specific matches**: one opportunity stored once; matches computed for each profile, owner-isolated; score and evidence persisted/versioned where necessary.
4. **Profile updates**: trigger controlled asynchronous re-evaluation after material profile changes; show freshness; avoid duplicate notifications.
5. **Dashboard**: source, job title, date, location, remote mode, salary if known, score, reasons, filters independent of notification threshold, original posting link; loading/empty/error states and mobile-responsive layout.
6. **E2E proof**: evidence of a real ingested job displayed for an authenticated user with traceable score and functioning external link.

## Not in this milestone
Market thermometer and skills gap dashboard; paid subscriptions; Fiverr; automatic applications; broad scraping; full API refactor; multi-source connectors; complete notification delivery.

## Architecture constraints
Keep .NET Minimal APIs, prefer endpoint groups/vertical slices and a thin Program.cs with extensions for DI/auth/pipeline/endpoints. Apply incrementally when touching code; no sweeping refactor. Do not assume React migration from current Blazor Web. Domain/Application/Infrastructure boundaries, tests, clean separation, documented decisions.

## UX quality gate
Frontend & UX Engineer owns initial visual language, accessibility, layout, responsiveness, component reuse and usable job search/filter states. Produce lightweight design guidance before implementation; no speculative full redesign.

## Definition of done
- Source legality/access documented and verified for intended usage.
- At least one real job ingested automatically without manual entry, deduplicated, with source provenance.
- Authenticated user sees ranked opportunities and an explanation of score; can visit original posting.
- Profile update can cause match re-evaluation; score metadata/freshness visible.
- Working tests for ingestion, normalization, matching isolation, and a smoke/E2E demonstration.
- No unverified claims of production readiness; screenshots and test evidence attached to delivery issue.

## Execution order
A. Source feasibility (critical path) + Frontend UX foundation (parallel)
B. Connector ingestion + catalog
C. Profile matching/re-evaluation
D. Dashboard + E2E validation

## Future backlog from vision
Career intelligence thermometer with sample/period/context; gaps ranked by frequency, impact, relevance and evidence; differentiate not-demonstrated from confirmed missing; independent notification thresholds and listing filters; optional Google/Microsoft Auth0 connections; mobile evolution; AI-assisted freelance listing/video subject to platform rules.

## Project governance
Create and link issues for each vertical deliverable; no work only in chat or agent memory. Orchestrator coordinates scope and dependencies, avoids agent overhead. Architect defines boundaries, Developer implements, Frontend UX designs/implements UI, Test Engineer validates, Code Reviewer reviews. Reference #45 for product intent.
