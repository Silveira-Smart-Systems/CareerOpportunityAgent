# COA — Agent Contracts and Execution Boundaries

Status: Proposed — design contract, not implementation  
Tracking: #5

## Conceptual C# contract
```csharp
public interface IAgent<in TInput, TOutput>
{
    string Name { get; }
    Task<AgentResult<TOutput>> ExecuteAsync(
        TInput input,
        AgentExecutionContext context,
        CancellationToken cancellationToken);
}

public sealed record AgentResult<T>(
    bool Success,
    T? Data,
    IReadOnlyCollection<AgentIssue> Issues,
    AgentMetadata Metadata);

public sealed record AgentIssue(
    string Code,
    string Message,
    AgentIssueSeverity Severity);

public sealed record AgentMetadata(
    string AgentName,
    string ModelId,
    string PromptVersion,
    TimeSpan Duration,
    string ExecutionId);
```

Supporting types (AgentExecutionContext, AgentIssueSeverity, request/output schemas) will be defined in implementation; the snippet illustrates boundaries only.

## Execution context
Immutable request-scoped context should carry: correlation/execution IDs, authenticated user scope, profile version, vacancy snapshot ID, scoring policy version, model routing policy version, budget/deadline, and approved capability set. Do not include raw secrets in agent context.

## Result semantics
- **Success** means schema-valid output, not automatically trustworthy factual content.
- **Issues** use stable codes for invalid inputs, missing evidence, provider unavailable, timeout, budget exceeded, policy violation, and human-review-needed.
- **Metadata** records model/prompt versions, latency, usage/cost if available, and traceability IDs.
- Null/unknown values must remain explicit rather than being invented to satisfy a schema.
- Cancellation propagates through model/provider calls.

## Persistence and side effects
Application use cases persist validated results; agents return proposals only. Provider adapters encapsulate remote access. Use idempotency keys for repeated executions. External model calls must not hold database transactions open.

## Review checkpoints
Human approval is mandatory for changing profile facts without confirmation, promoting new scoring policies, and any future outbound submission capability. No outbound submission interface is part of MVP.

## Tests
Schema validation, adversarial vacancy text, hallucinated skills, missing salary, contradictory remote requirements, timeout, cancellation, duplicate runs, and traceability of evidence.