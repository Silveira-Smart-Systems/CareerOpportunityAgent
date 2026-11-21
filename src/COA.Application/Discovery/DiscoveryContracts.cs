using System.Collections.Frozen;
using System.Collections.ObjectModel;
using COA.Domain.SearchProfiles;

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

public sealed record SearchRequest
{
    public SearchRequest(IEnumerable<CountryCode> sourceMarkets, IEnumerable<string> categories,
        IEnumerable<string> targetRoles, IEnumerable<string> workModes, CountryCode? remoteFromCountry,
        IEnumerable<SearchCriterion> mandatoryCriteria, IEnumerable<SearchCriterion> preferredCriteria)
        : this(sourceMarkets, categories, targetRoles, workModes, remoteFromCountry, [], null, [], [], [],
            mandatoryCriteria, preferredCriteria)
    {
    }

    public SearchRequest(IEnumerable<CountryCode> sourceMarkets, IEnumerable<string> categories,
        IEnumerable<string> targetRoles, IEnumerable<string> workModes, CountryCode? remoteFromCountry,
        IEnumerable<string> employmentTypes, SalaryPreference? salaryPreference, IEnumerable<string> languages,
        IEnumerable<string> skills, IEnumerable<string> excludedKeywords,
        IEnumerable<SearchCriterion> mandatoryCriteria, IEnumerable<SearchCriterion> preferredCriteria)
    {
        SourceMarkets = Freeze(sourceMarkets, nameof(sourceMarkets));
        Categories = FreezeText(categories, 100, nameof(categories));
        TargetRoles = FreezeText(targetRoles, 200, nameof(targetRoles));
        WorkModes = FreezeText(workModes, 100, nameof(workModes));
        RemoteFromCountry = remoteFromCountry;
        EmploymentTypes = FreezeText(employmentTypes, 100, nameof(employmentTypes));
        SalaryPreference = salaryPreference;
        Languages = FreezeText(languages, 100, nameof(languages));
        Skills = FreezeText(skills, 200, nameof(skills));
        ExcludedKeywords = FreezeText(excludedKeywords, 200, nameof(excludedKeywords));
        MandatoryCriteria = Freeze(mandatoryCriteria, nameof(mandatoryCriteria));
        PreferredCriteria = Freeze(preferredCriteria, nameof(preferredCriteria));
        Validate();
    }

    public IReadOnlySet<CountryCode> SourceMarkets { get; }
    public IReadOnlySet<string> Categories { get; }
    public IReadOnlySet<string> TargetRoles { get; }
    public IReadOnlySet<string> WorkModes { get; }
    public CountryCode? RemoteFromCountry { get; }
    public IReadOnlySet<string> EmploymentTypes { get; }
    public SalaryPreference? SalaryPreference { get; }
    public IReadOnlySet<string> Languages { get; }
    public IReadOnlySet<string> Skills { get; }
    public IReadOnlySet<string> ExcludedKeywords { get; }
    public IReadOnlySet<SearchCriterion> MandatoryCriteria { get; }
    public IReadOnlySet<SearchCriterion> PreferredCriteria { get; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(SourceMarkets);
        ArgumentNullException.ThrowIfNull(Categories);
        ArgumentNullException.ThrowIfNull(TargetRoles);
        ArgumentNullException.ThrowIfNull(WorkModes);
        ArgumentNullException.ThrowIfNull(EmploymentTypes);
        ArgumentNullException.ThrowIfNull(Languages);
        ArgumentNullException.ThrowIfNull(Skills);
        ArgumentNullException.ThrowIfNull(ExcludedKeywords);
        ArgumentNullException.ThrowIfNull(MandatoryCriteria);
        ArgumentNullException.ThrowIfNull(PreferredCriteria);

        if (SourceMarkets.Count == 0)
            throw new ArgumentException("At least one source market is required.", nameof(SourceMarkets));
        if (MandatoryCriteria.Overlaps(PreferredCriteria))
            throw new ArgumentException("A criterion cannot be both mandatory and preferred.");
        SalaryPreference?.Validate();
    }

    public bool HasSameIntent(SearchRequest other) => other is not null &&
        SourceMarkets.SetEquals(other.SourceMarkets) && Categories.SetEquals(other.Categories) &&
        TargetRoles.SetEquals(other.TargetRoles) && WorkModes.SetEquals(other.WorkModes) &&
        RemoteFromCountry == other.RemoteFromCountry && EmploymentTypes.SetEquals(other.EmploymentTypes) &&
        SalaryPreference == other.SalaryPreference && Languages.SetEquals(other.Languages) &&
        Skills.SetEquals(other.Skills) && ExcludedKeywords.SetEquals(other.ExcludedKeywords) &&
        MandatoryCriteria.SetEquals(other.MandatoryCriteria) &&
        PreferredCriteria.SetEquals(other.PreferredCriteria);

    private static IReadOnlySet<T> Freeze<T>(IEnumerable<T> values, string name) =>
        (values ?? throw new ArgumentNullException(name)).ToFrozenSet();
    private static IReadOnlySet<string> FreezeText(IEnumerable<string> values, int maximumLength, string name)
    {
        var copied = (values ?? throw new ArgumentNullException(name)).ToArray();
        if (copied.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Search values must be nonempty.", name);
        if (copied.Any(value => value.Trim().Length > maximumLength)) throw new ArgumentException("Search values exceed the supported length.", name);
        return copied.Select(value => value.Trim()).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
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

public sealed record PlannedSource
{
    public PlannedSource(SourceId sourceId, IEnumerable<CountryCode> selectedMarkets, bool isEligible,
        IEnumerable<SourcePlanReason> reasons, IEnumerable<PlannedCriterion> criteria)
    {
        SourceId = sourceId;
        SelectedMarkets = (selectedMarkets ?? throw new ArgumentNullException(nameof(selectedMarkets))).ToFrozenSet();
        IsEligible = isEligible;
        Reasons = Array.AsReadOnly((reasons ?? throw new ArgumentNullException(nameof(reasons))).ToArray());
        Criteria = Array.AsReadOnly((criteria ?? throw new ArgumentNullException(nameof(criteria))).ToArray());
    }
    public SourceId SourceId { get; }
    public IReadOnlySet<CountryCode> SelectedMarkets { get; }
    public bool IsEligible { get; }
    public IReadOnlyList<SourcePlanReason> Reasons { get; }
    public IReadOnlyList<PlannedCriterion> Criteria { get; }
}

public sealed record DiscoveryPlan
{
    public DiscoveryPlan(SearchRequest searchRequest, IEnumerable<PlannedSource> sources)
    {
        SearchRequest = searchRequest ?? throw new ArgumentNullException(nameof(searchRequest));
        var copied = (sources ?? throw new ArgumentNullException(nameof(sources))).OrderBy(source => source.SourceId.Value, StringComparer.Ordinal).ToArray();
        if (copied.Any(source => source is null) || copied.Select(source => source.SourceId).Distinct().Count() != copied.Length)
            throw new ArgumentException("A discovery plan requires unique, nonnull sources.", nameof(sources));
        Sources = Array.AsReadOnly(copied);
    }
    public SearchRequest SearchRequest { get; }
    public IReadOnlyList<PlannedSource> Sources { get; }
    public IReadOnlyList<PlannedSource> EligibleSources => Sources.Where(source => source.IsEligible).ToArray();
    public bool Matches(SearchRequest request) => SearchRequest.HasSameIntent(request);
}

public interface IDiscoveryPlanner
{
    DiscoveryPlan CreatePlan(SearchRequest request, DiscoveryPlanningSnapshot snapshot);
}
