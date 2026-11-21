using System.Collections.Frozen;
using System.Collections.ObjectModel;

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

public sealed record SourceCapabilities
{
    public SourceCapabilities(
        SourceId sourceId,
        IEnumerable<CountryCode> supportedSourceMarkets,
        IEnumerable<KeyValuePair<SearchCriterion, FilterSupport>> filters)
    {
        ArgumentNullException.ThrowIfNull(supportedSourceMarkets);
        ArgumentNullException.ThrowIfNull(filters);

        SourceId = sourceId;
        SupportedSourceMarkets = supportedSourceMarkets.ToFrozenSet();
        Filters = filters.ToFrozenDictionary();

        if (Filters.Values.Any(support => !Enum.IsDefined(support)))
            throw new ArgumentException("Filter support contains an invalid enum value.", nameof(filters));
    }

    public SourceId SourceId { get; }

    public IReadOnlySet<CountryCode> SupportedSourceMarkets { get; }

    public IReadOnlyDictionary<SearchCriterion, FilterSupport> Filters { get; }

    public FilterSupport GetFilterSupport(SearchCriterion criterion) =>
        Filters.TryGetValue(criterion, out var support)
            ? support
            : FilterSupport.Unsupported;
}

public sealed record SourceLicenseRights(
    bool AllowsDiscovery,
    bool AllowsRetention,
    bool AllowsRedistribution);

public sealed record RateLimitPolicy
{
    public RateLimitPolicy(int? maximumRequestsPerDay)
    {
        if (maximumRequestsPerDay is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRequestsPerDay));

        MaximumRequestsPerDay = maximumRequestsPerDay;
    }

    public int? MaximumRequestsPerDay { get; }
}

public sealed record SourceBudgetPolicy
{
    public SourceBudgetPolicy(decimal? maximumCostPerRequest)
    {
        if (maximumCostPerRequest is < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCostPerRequest));

        MaximumCostPerRequest = maximumCostPerRequest;
    }

    public decimal? MaximumCostPerRequest { get; }
}

public sealed record SourceExecutionPolicy
{
    public SourceExecutionPolicy(
        bool isAuthorized,
        SourceLicenseRights licenseRights,
        RateLimitPolicy rateLimit,
        SourceBudgetPolicy budget)
    {
        IsAuthorized = isAuthorized;
        LicenseRights = licenseRights ?? throw new ArgumentNullException(nameof(licenseRights));
        RateLimit = rateLimit ?? throw new ArgumentNullException(nameof(rateLimit));
        Budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    public bool IsAuthorized { get; }

    public SourceLicenseRights LicenseRights { get; }

    public RateLimitPolicy RateLimit { get; }

    public SourceBudgetPolicy Budget { get; }
}

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

public sealed record SourceRuntimeStatus
{
    public SourceRuntimeStatus(
        SourceHealth health,
        CapacityAvailability quota,
        CapacityAvailability budget,
        IEnumerable<string> temporaryRestrictions,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(temporaryRestrictions);

        if (!Enum.IsDefined(health) || !Enum.IsDefined(quota) || !Enum.IsDefined(budget))
            throw new ArgumentException("Runtime status contains an invalid enum value.");

        var restrictions = temporaryRestrictions.ToArray();
        if (restrictions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Temporary restrictions must be nonempty.", nameof(temporaryRestrictions));

        Health = health;
        Quota = quota;
        Budget = budget;
        TemporaryRestrictions = restrictions.ToFrozenSet(StringComparer.Ordinal);
        ObservedAtUtc = observedAtUtc;
    }

    public SourceHealth Health { get; }

    public CapacityAvailability Quota { get; }

    public CapacityAvailability Budget { get; }

    public IReadOnlySet<string> TemporaryRestrictions { get; }

    public DateTimeOffset ObservedAtUtc { get; }
}

public interface ISourceCapabilityRegistry
{
    IReadOnlyList<SourceCapabilities> GetAll();

    SourceCapabilities? Find(SourceId sourceId);
}

public sealed class SourceCapabilityRegistry(IEnumerable<SourceCapabilities> sources) : ISourceCapabilityRegistry
{
    private readonly IReadOnlyDictionary<SourceId, SourceCapabilities> _sources =
        (sources ?? throw new ArgumentNullException(nameof(sources)))
        .ToFrozenDictionary(source => source?.SourceId
            ?? throw new ArgumentException("A source capability is required.", nameof(sources)));

    public IReadOnlyList<SourceCapabilities> GetAll() =>
        new ReadOnlyCollection<SourceCapabilities>(_sources.Values
            .OrderBy(source => source.SourceId.Value, StringComparer.Ordinal)
            .ToList());

    public SourceCapabilities? Find(SourceId sourceId) =>
        _sources.GetValueOrDefault(sourceId);
}

public sealed record SourcePlanningState
{
    public SourcePlanningState(
        SourceCapabilities capabilities,
        SourceExecutionPolicy executionPolicy,
        SourceRuntimeStatus runtimeStatus)
    {
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        ExecutionPolicy = executionPolicy ?? throw new ArgumentNullException(nameof(executionPolicy));
        RuntimeStatus = runtimeStatus ?? throw new ArgumentNullException(nameof(runtimeStatus));
    }

    public SourceCapabilities Capabilities { get; }

    public SourceExecutionPolicy ExecutionPolicy { get; }

    public SourceRuntimeStatus RuntimeStatus { get; }
}

public sealed record DiscoveryPlanningSnapshot
{
    public DiscoveryPlanningSnapshot(IEnumerable<SourcePlanningState> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var copiedSources = sources.ToArray();
        if (copiedSources.Any(source => source is null))
            throw new ArgumentException("A planning snapshot cannot contain null sources.", nameof(sources));
        if (copiedSources.Select(source => source.Capabilities.SourceId).Distinct().Count() != copiedSources.Length)
            throw new ArgumentException("A planning snapshot cannot contain duplicate sources.", nameof(Sources));

        Sources = Array.AsReadOnly(copiedSources);
    }

    public IReadOnlyList<SourcePlanningState> Sources { get; }

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
