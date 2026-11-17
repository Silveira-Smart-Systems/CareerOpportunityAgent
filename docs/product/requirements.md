# COA — MVP Requirements

Status: Draft for review  
Tracking: #2

## Actors
- **User Zero:** first real user; configures profile and preferences, evaluates opportunities, supplies feedback, reviews drafts.
- **Scheduled Worker:** executes authorized ingestion and assessment jobs.
- **Agent Orchestrator:** coordinates specialized components; cannot bypass policy or human approval.

## Functional requirements
| ID | Requirement | Acceptance criterion |
|---|---|---|
| FR-01 | Import PDF/DOCX CV and extract structured profile | AC01 |
| FR-02 | Record skill/project evidence, confidence, and unknown/unproven status | AC02 |
| FR-03 | Discover opportunities through approved Upwork integration, when authorized | AC03 (conditional) |
| FR-04 | Manually import LinkedIn opportunity URL and description | AC04 |
| FR-05 | Normalize and deduplicate opportunities | AC05 |
| FR-06 | Calculate configurable five-dimension opportunity fit | AC06 |
| FR-07 | Explain assessments with evidence and scoring rationale | AC07 |
| FR-08 | Identify gaps and unproven mandatory requirements | AC08 |
| FR-09 | Generate daily ranked opportunity report | AC09 |
| FR-10 | Record human decision and rejection/acceptance reasons | AC10 |
| FR-11 | Prepare editable English application/proposal drafts | AC11 |
| FR-12 | Prevent automatic submission of applications/messages | AC12 |

## Acceptance criteria
- **AC01:** User can upload a supported CV and inspect/edit extracted structured profile data; extraction failures are reported.
- **AC02:** Each material extracted skill/experience claim includes its source or is marked unverified; no invented expertise is treated as proven.
- **AC03:** If Upwork API access is approved, ingestion uses permitted official access and captures source metadata; otherwise the feature is explicitly disabled with manual fallback.
- **AC04:** User can submit LinkedIn URL and vacancy text; the system stores source provenance without scraping LinkedIn.
- **AC05:** Re-importing the same provider ID or canonical source URL does not create duplicate active opportunities; ambiguous duplicates are reviewable.
- **AC06:** Assessment returns separate scores for Technical, Commercial, Work Model, Seniority, and Career Alignment, plus a reproducible weighted total for a fixed profile version, vacancy snapshot, and scoring policy version.
- **AC07:** Assessment exposes supporting evidence, relevant constraints, uncertainty, and scoring-policy version; explanations must not invent facts.
- **AC08:** Mandatory requirements are marked Matched, Missing, or Unknown/NotProven, with cited source evidence where available.
- **AC09:** A daily report lists ranked opportunities, assessment summaries, source links, and execution status; failed runs are visible.
- **AC10:** User can record decision, timestamp, and rationale; prior decisions remain auditable and available for evaluation.
- **AC11:** User can generate and edit English drafts grounded in confirmed profile information and vacancy text.
- **AC12:** No API, scheduled task, or agent in the MVP submits an application or sends recruiter messages; draft preparation is the only supported action.

## Initial scoring policy (hypothesis)
| Dimension | Weight |
|---|---:|
| Technical fit | 40% |
| Commercial fit | 25% |
| Work-model fit | 15% |
| Seniority fit | 10% |
| Career alignment | 10% |

Indicative categories: 85–100 Excellent; 70–84 Good; 55–69 Moderate; 0–54 Low. Hard constraints (e.g., incompatible onsite requirement or compensation below a configured minimum) may block or downgrade recommendations, independently of the numeric total. Weights, categories, and constraint rules are versioned and configurable.

## Non-functional requirements
- **NFR-01 Security:** protect CV data and credentials; secret stores in deployment, no secrets committed.
- **NFR-02 Privacy:** minimize and redact PII before third-party model calls where possible; define retention/deletion before beta.
- **NFR-03 Integrity:** persist source references, profile version, vacancy snapshot, policy version, model/prompt version, and human decisions.
- **NFR-04 Reliability:** retries with bounded backoff, deduplication, recoverable scheduled jobs, visible failures.
- **NFR-05 Observability:** correlation IDs, structured logs, latency/token/cost metadata, without logging secrets or unnecessary PII.
- **NFR-06 Safety:** treat CVs and vacancy descriptions as untrusted data; never execute embedded instructions or bypass approval.
- **NFR-07 Testability:** deterministic scoring unit tests, integration tests, architecture tests, and agent evaluation fixtures.
- **NFR-08 Maintainability:** modular-monolith boundaries and provider-neutral interfaces; avoid unnecessary service fragmentation.
- **NFR-09 Performance:** capture baseline extraction/assessment latency and cost before defining production SLOs.
- **NFR-10 Compliance:** review terms of service and permitted uses for each opportunity provider.

## Initial profile preferences (User Zero — configurable)
- Priority for fully remote roles; hybrid/onsite should be treated as potential hard constraints according to explicit settings.
- Focus on senior .NET technical leadership, architecture, Azure, and distributed systems.
- Commercial thresholds must be user-configurable and currency-aware; do not hard-code assumptions about acceptable compensation.
- English-language application drafts are supported; language proficiency is not inferred beyond verified information.

## Dependencies and open questions
- Official Upwork API eligibility, scopes, quota, and permitted use.
- Which profile fields may be sent to external model providers.
- What constitutes acceptable recommendation precision and time savings.
- How compensation is normalized for hourly, daily, monthly, and annual roles.
- What evidence is required to declare a mandatory skill Missing versus Unknown.

## Traceability
Every implementation Issue/PR should reference relevant FR/AC/NFR IDs. Product decisions and changes to acceptance criteria require versioned documentation review.