# COA — Components, Contracts and Boundaries

Status: Proposed / Draft  
Tracking: #3; agent-contract detail belongs to #5

## Proposed solution layout
```text
src/
  COA.Api/
  COA.Application/
  COA.Domain/
  COA.Infrastructure/
  COA.Worker/
  COA.Modules.Profile/
  COA.Modules.Opportunities/
  COA.Modules.Matching/
  COA.Modules.Applications/
  COA.Modules.Agents/
web/
  coa-web/
tests/
  COA.UnitTests/
  COA.IntegrationTests/
  COA.ArchitectureTests/
  COA.AgentEvaluationTests/
docs/
  product/
  architecture/
  agents/
```

This is a **candidate layout**, not a mandate to create an assembly for every module. During implementation, ensure no cyclic references or parallel duplicate domain models.

## Dependency rules
- Domain owns business invariants and domain types; no references to Infrastructure, Hangfire, React, or NVIDIA SDKs.
- Application owns use cases, orchestration, interfaces/ports, and transaction boundaries.
- Infrastructure owns SQL Server repositories, file storage, opportunity adapters, and AI provider clients.
- API and Worker are composition roots, with no business rules embedded in controllers or scheduled job classes.
- Module-to-module communication uses explicit application contracts; no cross-module writes to another module's tables.
- Agents consume typed application inputs and return typed validated outputs; they do not query the DbContext directly.

## Key ports (conceptual)
```csharp
public interface IOpportunitySource
{
    string Provider { get; }
    Task<OpportunitySearchResult> SearchAsync(
        OpportunitySearchCriteria criteria,
        CancellationToken cancellationToken);
}

public interface IAgent<TInput, TOutput>
{
    string Name { get; }
    Task<AgentResult<TOutput>> ExecuteAsync(
        TInput input,
        AgentExecutionContext context,
        CancellationToken cancellationToken);
}
```

Types are placeholders for the contracts to be formalized in #5; these snippets are not compile-ready implementations.

## Persistence and ownership
| Data | Owner | Notes |
|---|---|---|
| ProfessionalProfile, ProfileSkill, ProjectExperience, CareerPreference, ProfessionalConstraint | Profile | Profile version and provenance |
| Opportunity, OpportunitySource, OpportunityRequirement | Opportunities | Canonical source identity and immutable ingestion snapshots |
| OpportunityAssessment, MatchEvidence, MatchGap, ScoringPolicy | Matching | Snapshot and policy version |
| ApplicationDraft | Applications | No submission status/action in MVP |
| AgentExecution, ModelUsage | Agents | Correlation, model/prompt version, latency/cost |
| DailyReport, UserDecision | Reporting / application workflows | Retain decision history and execution state |

SQL Server is the system of record. Vector representations, if introduced, are derived and rebuildable. No external model output is authoritative until validated.

## Scoring responsibilities
1. Requirement extraction proposes structured, source-linked requirements.
2. Structured matching compares requirements against verified profile evidence.
3. Commercial and work-model checks evaluate explicit preferences/constraints.
4. Critical Reviewer identifies unsupported assumptions and potential violations.
5. Deterministic scoring engine applies versioned weights and constraint policy.
6. Persist assessment with profile version, vacancy snapshot, policy version, and model/prompt metadata.

The model cannot silently change scoring weights or bypass hard constraints.

## Error and transaction boundaries
- Persist ingestion and assessment state transitions independently so retries can resume safely.
- External calls are outside long-running database transactions.
- Outbox is considered only for durable cross-boundary events where needed; avoid adding a message broker by default.
- Define retryable vs terminal errors and a manual reprocessing path.
- Use optimistic concurrency where user edits could conflict with background assessment.

## Test strategy
- Unit: deterministic scoring, hard constraints, normalization/dedup rules.
- Integration: SQL repositories, transaction/idempotency behavior, adapters with mocks/fakes.
- Architecture: forbidden dependencies and module boundaries.
- Agent evaluation: fixed CV/vacancy fixtures, provenance, hallucination, and false-positive checks.
- Security: untrusted content/prompt-injection regression tests.

## Related work
- #2: product requirements and validation.
- #4: ADRs and architectural trade-offs.
- #5: typed agent contracts, prompts, model routing.
- #6: CI and quality gates.