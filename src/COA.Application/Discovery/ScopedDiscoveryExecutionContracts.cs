namespace COA.Application.Discovery;

/// <summary>
/// Scoped execution is deliberately separate from the legacy country-market adapter.
/// A planning result never authorizes a call; only the scoped gate can reserve a permit.
/// </summary>
public sealed class ScopedSourceExecutionRequest
{
    public ScopedSourceExecutionRequest(ScopedSourceExecutionTarget target, SearchRequest filters,
        IEnumerable<PlannedCriterion> criteria)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Filters = filters ?? throw new ArgumentNullException(nameof(filters));
        Filters.Validate();
        var copied = (criteria ?? throw new ArgumentNullException(nameof(criteria))).ToArray();
        if (copied.Any(x => x is null))
            throw new ArgumentException("Null criteria are not allowed.", nameof(criteria));
        if (copied.Select(x => x.Criterion).Distinct().Count() != copied.Length)
            throw new ArgumentException("Duplicate criteria are not allowed.", nameof(criteria));
        if (!Filters.MandatoryCriteria.All(x => copied.Any(y => y.Criterion == x && y.IsMandatory)) ||
            !Filters.PreferredCriteria.All(x => copied.Any(y => y.Criterion == x && !y.IsMandatory)) ||
            copied.Any(x => x.IsMandatory ? !Filters.MandatoryCriteria.Contains(x.Criterion) :
                !Filters.PreferredCriteria.Contains(x.Criterion)))
            throw new ArgumentException("Criteria do not match the request.", nameof(criteria));
        if (copied.Any(x => x.IsMandatory && x.Disposition is not
            (CriterionDisposition.AppliedByProvider or CriterionDisposition.AppliedPostFetch)))
            throw new ArgumentException("Mandatory criteria must be executable.", nameof(criteria));
        Criteria = Array.AsReadOnly(copied);
    }

    public ScopedSourceExecutionTarget Target { get; }
    public SearchRequest Filters { get; }
    public IReadOnlyList<PlannedCriterion> Criteria { get; }
}

/// <summary>
/// The gate must atomically revalidate CURRENT evidence-backed scope registration,
/// policy, licensing, health, capacity and budget before reserving resources.
/// An evidence-reference string or planning-time eligibility is not authorization.
/// </summary>
public interface IScopedSourceExecutionGate
{
    Task<ScopedSourceExecutionAuthorization> AuthorizeAndReserveAsync(
        ScopedSourceExecutionRequest request, CancellationToken cancellationToken);
}

public abstract record ScopedSourceExecutionAuthorization
{
    private ScopedSourceExecutionAuthorization() { }
    public sealed record Granted(ScopedSourceExecutionPermit Permit) : ScopedSourceExecutionAuthorization;
    public sealed record Blocked(ScopedSourceExecutionBlockReason Reason) : ScopedSourceExecutionAuthorization;
}

public enum ScopedSourceExecutionBlockReason
{
    UnsupportedScope,
    EvidenceNotVerified,
    NotAuthorized,
    LicenseDenied,
    SourceUnavailable,
    QuotaUnavailable,
    BudgetUnavailable,
    TemporarilyRestricted,
    ReservationUnavailable
}

public abstract class ScopedSourceExecutionPermit
{
    public abstract ScopedSourceExecutionTarget Target { get; }
    public abstract Task MarkAttemptStartedAsync(CancellationToken cancellationToken);
    public abstract Task ConsumeAsync(CancellationToken cancellationToken);
    public abstract Task ReleaseAsync(CancellationToken cancellationToken);
}

public interface IScopedOpportunitySourceAdapter
{
    SourceId SourceId { get; }
    Task<ScopedSourceSearchResult> SearchAsync(
        ScopedSourceExecutionRequest request, CancellationToken cancellationToken);
}

public interface IScopedOpportunitySourceAdapterResolver
{
    IScopedOpportunitySourceAdapter? Find(SourceId sourceId);
}

/// <summary>
/// Provenance records the actual requested scope, not a fabricated country market.
/// Field-level provenance evolution is reserved for Phase 4C.2-D.
/// </summary>
public sealed record ScopedSourceProvenance(
    SourceId SourceId, string ExternalId, Uri SourceUrl, SourceExecutionScope RequestedScope,
    DateTimeOffset RetrievedAtUtc)
{
    /// <summary>
    /// Optional provider-reported geography. Null means no geography claim was
    /// supplied; it must never be inferred from RequestedScope.
    /// </summary>
    public ScopedSourceGeographyClaim? GeographyClaim { get; init; }
}

public sealed record ScopedSourceOpportunityCandidate(
    ScopedSourceProvenance Provenance, IReadOnlyDictionary<string, SourceObservedValue> Fields);

public sealed record ScopedSourceSearchResult(
    IReadOnlyList<ScopedSourceOpportunityCandidate> Candidates, IReadOnlyList<AppliedCriterion> AppliedCriteria);

public sealed record ScopedSourceExecutionDiagnostic(
    ScopedSourceExecutionTarget Target, string Code);

public sealed record ScopedSourceExecutionOutcome(
    ScopedSourceExecutionTarget Target, SourceExecutionStatus Status,
    IReadOnlyList<ScopedSourceOpportunityCandidate> Candidates,
    IReadOnlyList<ScopedSourceExecutionDiagnostic> Diagnostics);

public sealed record ScopedDiscoveryOutcome(IReadOnlyList<ScopedSourceExecutionOutcome> Sources)
{
    public IReadOnlyList<ScopedSourceOpportunityCandidate> Candidates =>
        Sources.SelectMany(source => source.Candidates).ToArray();
}
