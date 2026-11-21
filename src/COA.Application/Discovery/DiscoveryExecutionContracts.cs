namespace COA.Application.Discovery;

public sealed record DiscoveryExecutionOptions(int MaxConcurrentSourceExecutions, TimeSpan SourceTimeout)
{
    public void Validate()
    {
        if (MaxConcurrentSourceExecutions < 1) throw new ArgumentOutOfRangeException(nameof(MaxConcurrentSourceExecutions));
        if (SourceTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(SourceTimeout));
    }
}

public sealed record SourceExecutionTarget(SourceId SourceId, CountryCode Market, SearchRequest SearchRequest,
    IReadOnlyList<PlannedCriterion> Criteria);

public enum SourceExecutionBlockReason
{
    NotAuthorized, LicenseDenied, SourceUnavailable, QuotaUnavailable, BudgetUnavailable,
    TemporarilyRestricted, ReservationUnavailable
}

public abstract record SourceExecutionAuthorization
{
    private SourceExecutionAuthorization() { }
    public sealed record Granted(SourceExecutionPermit Permit) : SourceExecutionAuthorization;
    public sealed record Blocked(SourceExecutionBlockReason Reason) : SourceExecutionAuthorization;
}

public abstract class SourceExecutionPermit
{
    public abstract SourceId SourceId { get; }
    public abstract Task MarkAttemptStartedAsync(CancellationToken cancellationToken);
    public abstract Task ConsumeAsync(CancellationToken cancellationToken);
    public abstract Task ReleaseAsync(CancellationToken cancellationToken);
}

public interface ISourceExecutionGate
{
    Task<SourceExecutionAuthorization> AuthorizeAndReserveAsync(SourceExecutionTarget target, CancellationToken cancellationToken);
}

public interface IOpportunitySourceAdapter
{
    SourceId SourceId { get; }
    Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken cancellationToken);
}

public interface IOpportunitySourceAdapterResolver
{
    IOpportunitySourceAdapter? Find(SourceId sourceId);
}

public enum CriterionApplication { AppliedByProvider, AppliedPostFetch }
public sealed record AppliedCriterion(SearchCriterion Criterion, CriterionApplication Application);
public enum SourceFieldStatus { Present, Unknown, Unavailable, Truncated }
public sealed record SourceObservedValue(string? Value, SourceFieldStatus Status);
public sealed record SourceProvenance(SourceId SourceId, string ExternalId, Uri SourceUrl, CountryCode RequestedMarket, DateTimeOffset RetrievedAtUtc);
public sealed record SourceOpportunityCandidate(SourceProvenance Provenance, IReadOnlyDictionary<string, SourceObservedValue> Fields);
public sealed record SourceSearchResult(IReadOnlyList<SourceOpportunityCandidate> Candidates, IReadOnlyList<AppliedCriterion> AppliedCriteria);

public enum SourceExecutionStatus { Succeeded, Blocked, AdapterMissing, TimedOut, AdapterCancelled, Failed, InvalidPlan }
public sealed record SourceExecutionDiagnostic(SourceId SourceId, CountryCode Market, string Code);
public sealed record SourceExecutionOutcome(SourceId SourceId, CountryCode Market, SourceExecutionStatus Status,
    IReadOnlyList<SourceOpportunityCandidate> Candidates, IReadOnlyList<SourceExecutionDiagnostic> Diagnostics);
public sealed record DiscoveryOutcome(IReadOnlyList<SourceExecutionOutcome> Sources)
{
    public IReadOnlyList<SourceOpportunityCandidate> Candidates => Sources.SelectMany(source => source.Candidates).ToArray();
}

public interface IDiscoveryOrchestrator
{
    Task<DiscoveryOutcome> ExecuteAsync(SearchRequest request, DiscoveryPlan plan, DiscoveryExecutionOptions options,
        CancellationToken cancellationToken = default);
}
