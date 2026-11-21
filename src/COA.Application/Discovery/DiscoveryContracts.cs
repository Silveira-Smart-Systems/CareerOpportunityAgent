namespace COA.Application.Discovery;

public readonly record struct SourceId
{
    public SourceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A source identifier is required.", nameof(value));

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CountryCode
{
    public CountryCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 2 ||
            !value.Trim().All(char.IsAsciiLetter))
            throw new ArgumentException("An ISO 3166-1 alpha-2 country code is required.", nameof(value));

        Value = value.Trim().ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum SearchCriterion
{
    Category,
    TargetRole,
    WorkMode,
    RemoteFromCountry,
    Contract,
    Salary,
    Language,
    Skill,
    ExcludedKeyword
}

public enum FilterSupport
{
    SupportedByProvider,
    SafePostFilter,
    Unsupported,
    UnknownSemantics
}

public sealed record SearchRequest(
    IReadOnlySet<CountryCode> SourceMarkets,
    IReadOnlySet<string> Categories,
    IReadOnlySet<string> TargetRoles,
    IReadOnlySet<string> WorkModes,
    CountryCode? RemoteFromCountry,
    IReadOnlySet<SearchCriterion> MandatoryCriteria,
    IReadOnlySet<SearchCriterion> PreferredCriteria)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(SourceMarkets);
        ArgumentNullException.ThrowIfNull(Categories);
        ArgumentNullException.ThrowIfNull(TargetRoles);
        ArgumentNullException.ThrowIfNull(WorkModes);
        ArgumentNullException.ThrowIfNull(MandatoryCriteria);
        ArgumentNullException.ThrowIfNull(PreferredCriteria);

        if (SourceMarkets.Count == 0)
            throw new ArgumentException("At least one source market is required.", nameof(SourceMarkets));
        if (MandatoryCriteria.Overlaps(PreferredCriteria))
            throw new ArgumentException("A criterion cannot be both mandatory and preferred.");
    }
}

public sealed record SourceCapabilities(
    SourceId SourceId,
    IReadOnlySet<CountryCode> SupportedSourceMarkets,
    IReadOnlyDictionary<SearchCriterion, FilterSupport> Filters)
{
    public FilterSupport GetFilterSupport(SearchCriterion criterion) =>
        Filters.TryGetValue(criterion, out var support)
            ? support
            : FilterSupport.Unsupported;
}

public sealed record SourceLicenseRights(
    bool AllowsDiscovery,
    bool AllowsRetention,
    bool AllowsRedistribution);

public sealed record RateLimitPolicy(int? MaximumRequestsPerDay);

public sealed record SourceBudgetPolicy(decimal? MaximumCostPerRequest);

public sealed record SourceExecutionPolicy(
    bool IsAuthorized,
    SourceLicenseRights LicenseRights,
    RateLimitPolicy RateLimit,
    SourceBudgetPolicy Budget);

public enum SourceHealth
{
    Healthy,
    Degraded,
    Unavailable
}

public enum CapacityAvailability
{
    Available,
    Exhausted,
    Unknown
}

public sealed record SourceRuntimeStatus(
    SourceHealth Health,
    CapacityAvailability Quota,
    CapacityAvailability Budget,
    IReadOnlyList<string> TemporaryRestrictions,
    DateTimeOffset ObservedAtUtc);

public interface ISourceCapabilityRegistry
{
    IReadOnlyList<SourceCapabilities> GetEnabled();

    SourceCapabilities? Find(SourceId sourceId);
}

public sealed class SourceCapabilityRegistry(IEnumerable<SourceCapabilities> sources) : ISourceCapabilityRegistry
{
    private readonly IReadOnlyDictionary<SourceId, SourceCapabilities> _sources =
        (sources ?? throw new ArgumentNullException(nameof(sources)))
        .ToDictionary(source => source.SourceId);

    public IReadOnlyList<SourceCapabilities> GetEnabled() => _sources.Values.ToArray();

    public SourceCapabilities? Find(SourceId sourceId) =>
        _sources.GetValueOrDefault(sourceId);
}

public sealed record SourcePlanningState(
    SourceCapabilities Capabilities,
    SourceExecutionPolicy ExecutionPolicy,
    SourceRuntimeStatus RuntimeStatus);

public sealed record DiscoveryPlanningSnapshot(IReadOnlyList<SourcePlanningState> Sources)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Sources);
        if (Sources.Select(source => source.Capabilities.SourceId).Distinct().Count() != Sources.Count)
            throw new ArgumentException("A planning snapshot cannot contain duplicate sources.", nameof(Sources));
    }
}

public enum CriterionDisposition
{
    AppliedByProvider,
    AppliedPostFetch,
    Unsupported,
    UnknownSemantics
}

public enum SourcePlanReason
{
    Eligible,
    UnsupportedMarket,
    MandatoryCriterionUnsupported,
    NotAuthorized,
    LicenseDoesNotAllowDiscovery,
    SourceUnavailable,
    QuotaUnavailable,
    BudgetUnavailable,
    TemporarilyRestricted
}

public sealed record PlannedCriterion(
    SearchCriterion Criterion,
    bool IsMandatory,
    CriterionDisposition Disposition);

public sealed record PlannedSource(
    SourceId SourceId,
    IReadOnlySet<CountryCode> SelectedMarkets,
    bool IsEligible,
    IReadOnlyList<SourcePlanReason> Reasons,
    IReadOnlyList<PlannedCriterion> Criteria);

public sealed record DiscoveryPlan(IReadOnlyList<PlannedSource> Sources)
{
    public IReadOnlyList<PlannedSource> EligibleSources => Sources.Where(source => source.IsEligible).ToArray();
}

public interface IDiscoveryPlanner
{
    DiscoveryPlan CreatePlan(SearchRequest request, DiscoveryPlanningSnapshot snapshot);
}
