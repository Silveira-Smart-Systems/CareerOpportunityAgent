using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class DiscoveryPlannerTests
{
    private static readonly CountryCode Portugal = new("PT");
    private static readonly CountryCode UnitedKingdom = new("GB");
    private static readonly CountryCode Brazil = new("BR");

    [Fact]
    public void Portugal_only_excludes_Adzuna_without_selecting_a_market()
    {
        var plan = CreatePlan(
            Request(markets: Markets(Portugal)),
            Adzuna());

        var source = Assert.Single(plan.Sources);
        Assert.False(source.IsEligible);
        Assert.Empty(source.SelectedMarkets);
        Assert.Contains(SourcePlanReason.UnsupportedMarket, source.Reasons);
    }

    [Fact]
    public void Great_Britain_only_selects_Adzuna_when_all_conditions_are_met()
    {
        var plan = CreatePlan(
            Request(markets: Markets(UnitedKingdom)),
            Adzuna());

        var source = Assert.Single(plan.EligibleSources);
        Assert.Equal(new SourceId("adzuna"), source.SourceId);
        Assert.Contains(UnitedKingdom, source.SelectedMarkets);
        Assert.Single(source.SelectedMarkets);
        Assert.Contains(SourcePlanReason.Eligible, source.Reasons);
    }

    [Fact]
    public void Portugal_and_international_select_only_eligible_market_source_pairs()
    {
        var plan = CreatePlan(
            Request(markets: Markets(Portugal, UnitedKingdom)),
            Adzuna(),
            PortugalProvider());

        Assert.Equal(2, plan.EligibleSources.Count);
        Assert.Collection(
            plan.EligibleSources,
            source =>
            {
                Assert.Equal(new SourceId("adzuna"), source.SourceId);
                Assert.Contains(UnitedKingdom, source.SelectedMarkets);
                Assert.Single(source.SelectedMarkets);
            },
            source =>
            {
                Assert.Equal(new SourceId("portugal-provider"), source.SourceId);
                Assert.Contains(Portugal, source.SelectedMarkets);
                Assert.Single(source.SelectedMarkets);
            });
    }

    [Fact]
    public void No_eligible_source_returns_an_empty_eligible_set_with_diagnostics()
    {
        var plan = CreatePlan(
            Request(markets: Markets(Portugal)),
            Adzuna(),
            PortugalProvider(runtime: Runtime(quota: CapacityAvailability.Exhausted)));

        Assert.Empty(plan.EligibleSources);
        Assert.Contains(SourcePlanReason.UnsupportedMarket, plan.Sources[0].Reasons);
        Assert.Contains(SourcePlanReason.QuotaUnavailable, plan.Sources[1].Reasons);
    }

    [Fact]
    public void Mandatory_unsupported_filter_excludes_the_source_instead_of_silently_widening()
    {
        var request = Request(
            markets: Markets(UnitedKingdom),
            mandatory: Criteria(SearchCriterion.RemoteFromCountry));

        var plan = CreatePlan(
            request,
            Adzuna(filters: new Dictionary<SearchCriterion, FilterSupport>
            {
                [SearchCriterion.Category] = FilterSupport.SupportedByProvider,
                [SearchCriterion.TargetRole] = FilterSupport.SupportedByProvider,
                [SearchCriterion.WorkMode] = FilterSupport.SafePostFilter,
                [SearchCriterion.RemoteFromCountry] = FilterSupport.UnknownSemantics
            }));

        var source = Assert.Single(plan.Sources);
        Assert.False(source.IsEligible);
        Assert.Contains(SourcePlanReason.MandatoryCriterionUnsupported, source.Reasons);
        var criterion = Assert.Single(source.Criteria, item =>
            item.Criterion == SearchCriterion.RemoteFromCountry);
        Assert.Equal(CriterionDisposition.UnknownSemantics, criterion.Disposition);
    }

    [Theory]
    [InlineData(false, true, SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, SourcePlanReason.NotAuthorized)]
    [InlineData(true, false, SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, SourcePlanReason.LicenseDoesNotAllowDiscovery)]
    [InlineData(true, true, SourceHealth.Unavailable, CapacityAvailability.Available, CapacityAvailability.Available, SourcePlanReason.SourceUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Exhausted, CapacityAvailability.Available, SourcePlanReason.QuotaUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Exhausted, SourcePlanReason.BudgetUnavailable)]
    public void Non_eligible_operational_conditions_exclude_a_source(
        bool authorized,
        bool licensed,
        SourceHealth health,
        CapacityAvailability quota,
        CapacityAvailability budget,
        SourcePlanReason expectedReason)
    {
        var plan = CreatePlan(
            Request(markets: Markets(UnitedKingdom)),
            Adzuna(
                policy: Policy(authorized, licensed),
                runtime: Runtime(health, quota, budget)));

        var source = Assert.Single(plan.Sources);
        Assert.False(source.IsEligible);
        Assert.Contains(expectedReason, source.Reasons);
    }

    [Fact]
    public void Temporary_restriction_excludes_a_source()
    {
        var plan = CreatePlan(
            Request(markets: Markets(UnitedKingdom)),
            Adzuna(runtime: new SourceRuntimeStatus(
                SourceHealth.Healthy,
                CapacityAvailability.Available,
                CapacityAvailability.Available,
                ["Maintenance window"],
                DateTimeOffset.UnixEpoch)));

        var source = Assert.Single(plan.Sources);
        Assert.False(source.IsEligible);
        Assert.Contains(SourcePlanReason.TemporarilyRestricted, source.Reasons);
    }

    [Fact]
    public void Same_request_and_snapshot_produce_the_same_plan()
    {
        var request = Request(markets: Markets(Portugal, UnitedKingdom));
        var planner = new DiscoveryPlanner();

        var first = planner.CreatePlan(request, Snapshot(Adzuna(), PortugalProvider()));
        var second = planner.CreatePlan(request, Snapshot(PortugalProvider(), Adzuna()));

        AssertPlansAreEquivalent(first, second);
    }

    [Fact]
    public void Snapshot_is_not_affected_by_mutating_input_collections()
    {
        var markets = new HashSet<CountryCode> { UnitedKingdom };
        var filters = new Dictionary<SearchCriterion, FilterSupport>
        {
            [SearchCriterion.Category] = FilterSupport.SupportedByProvider,
            [SearchCriterion.TargetRole] = FilterSupport.SupportedByProvider
        };
        var restrictions = new List<string>();
        var source = Source(
            "mutable-input-source",
            markets,
            filters,
            Policy(),
            new SourceRuntimeStatus(
                SourceHealth.Healthy,
                CapacityAvailability.Available,
                CapacityAvailability.Available,
                restrictions,
                DateTimeOffset.UnixEpoch));
        var sources = new[] { source };
        var snapshot = Snapshot(sources);

        markets.Clear();
        filters[SearchCriterion.TargetRole] = FilterSupport.Unsupported;
        restrictions.Add("Maintenance window");
        sources[0] = PortugalProvider();

        var plan = new DiscoveryPlanner().CreatePlan(Request(markets: Markets(UnitedKingdom)), snapshot);

        var plannedSource = Assert.Single(plan.EligibleSources);
        Assert.Equal(new SourceId("mutable-input-source"), plannedSource.SourceId);
        Assert.Contains(UnitedKingdom, plannedSource.SelectedMarkets);
        Assert.DoesNotContain(SourcePlanReason.TemporarilyRestricted, plannedSource.Reasons);
        Assert.Equal(
            CriterionDisposition.AppliedByProvider,
            plannedSource.Criteria.Single(criterion => criterion.Criterion == SearchCriterion.TargetRole).Disposition);
    }

    [Fact]
    public void Registry_finds_capabilities_without_provider_dependencies()
    {
        var capabilities = Adzuna().Capabilities;
        ISourceCapabilityRegistry registry = new SourceCapabilityRegistry(new[] { capabilities });

        Assert.Equal(capabilities, registry.Find(new SourceId("adzuna")));
        Assert.Null(registry.Find(new SourceId("unknown")));
    }

    [Fact]
    public void Discovery_foundation_has_no_HTTP_or_provider_SDK_assembly_dependency()
    {
        var references = typeof(DiscoveryPlanner).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("System.Net.Http", references);
        Assert.DoesNotContain("Adzuna", references);
    }

    private static DiscoveryPlan CreatePlan(SearchRequest request, params SourcePlanningState[] sources) =>
        new DiscoveryPlanner().CreatePlan(request, Snapshot(sources));

    private static DiscoveryPlanningSnapshot Snapshot(params SourcePlanningState[] sources) => new(sources);

    private static IReadOnlySet<CountryCode> Markets(params CountryCode[] countries) =>
        new HashSet<CountryCode>(countries);

    private static IReadOnlySet<SearchCriterion> Criteria(params SearchCriterion[] criteria) =>
        new HashSet<SearchCriterion>(criteria);

    private static SearchRequest Request(
        IReadOnlySet<CountryCode> markets,
        IReadOnlySet<SearchCriterion>? mandatory = null) =>
        new(
            markets,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Technology" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Software Architect", "Technical Lead" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Remote" },
            Portugal,
            mandatory ?? new HashSet<SearchCriterion>
            {
                SearchCriterion.Category,
                SearchCriterion.TargetRole
            },
            Criteria(SearchCriterion.WorkMode));

    // Simulated planning capabilities only; this is not a declaration of real API support.
    private static SourcePlanningState Adzuna(
        IReadOnlyDictionary<SearchCriterion, FilterSupport>? filters = null,
        SourceExecutionPolicy? policy = null,
        SourceRuntimeStatus? runtime = null) =>
        Source(
            "adzuna",
            Markets(UnitedKingdom, Brazil),
            filters ?? DefaultFilters(),
            policy ?? Policy(),
            runtime ?? Runtime());

    private static SourcePlanningState PortugalProvider(
        SourceRuntimeStatus? runtime = null) =>
        Source(
            "portugal-provider",
            Markets(Portugal),
            DefaultFilters(),
            Policy(),
            runtime ?? Runtime());

    private static SourcePlanningState Source(
        string sourceId,
        IReadOnlySet<CountryCode> markets,
        IReadOnlyDictionary<SearchCriterion, FilterSupport> filters,
        SourceExecutionPolicy policy,
        SourceRuntimeStatus runtime) =>
        new(
            new SourceCapabilities(new SourceId(sourceId), markets, filters),
            policy,
            runtime);

    private static IReadOnlyDictionary<SearchCriterion, FilterSupport> DefaultFilters() =>
        new Dictionary<SearchCriterion, FilterSupport>
        {
            [SearchCriterion.Category] = FilterSupport.SupportedByProvider,
            [SearchCriterion.TargetRole] = FilterSupport.SupportedByProvider,
            [SearchCriterion.WorkMode] = FilterSupport.UnknownSemantics
        };

    private static SourceExecutionPolicy Policy(bool authorized = true, bool licensed = true) =>
        new(
            authorized,
            new SourceLicenseRights(licensed, false, false),
            new RateLimitPolicy(500),
            new SourceBudgetPolicy(1m));

    private static SourceRuntimeStatus Runtime(
        SourceHealth health = SourceHealth.Healthy,
        CapacityAvailability quota = CapacityAvailability.Available,
        CapacityAvailability budget = CapacityAvailability.Available) =>
        new(health, quota, budget, Array.Empty<string>(), DateTimeOffset.UnixEpoch);

    private static void AssertPlansAreEquivalent(DiscoveryPlan expected, DiscoveryPlan actual)
    {
        Assert.Equal(expected.Sources.Count, actual.Sources.Count);

        foreach (var (expectedSource, actualSource) in expected.Sources.Zip(actual.Sources))
        {
            Assert.Equal(expectedSource.SourceId, actualSource.SourceId);
            Assert.Equal(expectedSource.IsEligible, actualSource.IsEligible);
            Assert.Equal(
                expectedSource.SelectedMarkets.OrderBy(market => market.Value),
                actualSource.SelectedMarkets.OrderBy(market => market.Value));
            Assert.Equal(expectedSource.Reasons, actualSource.Reasons);
            Assert.Equal(expectedSource.Criteria.Count, actualSource.Criteria.Count);

            foreach (var (expectedCriterion, actualCriterion) in expectedSource.Criteria.Zip(actualSource.Criteria))
            {
                Assert.Equal(expectedCriterion.Criterion, actualCriterion.Criterion);
                Assert.Equal(expectedCriterion.IsMandatory, actualCriterion.IsMandatory);
                Assert.Equal(expectedCriterion.Disposition, actualCriterion.Disposition);
            }
        }
    }
}
