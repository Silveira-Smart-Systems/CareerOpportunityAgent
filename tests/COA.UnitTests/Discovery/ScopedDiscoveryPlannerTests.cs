using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedDiscoveryPlannerTests
{
    private static readonly CountryCode Pt = new("PT");
    private static readonly CountryCode Gb = new("GB");

    [Fact]
    public void Publication_country_never_matches_a_legacy_market()
    {
        var plan = Plan(PublicationMarketIntent.KnownCountries([Pt]),
            [new SourceScopeCapability(new SourceExecutionScope.LegacyMarket(Pt), "verified-reference")]);
        var target = Assert.Single(plan.Targets);
        Assert.IsType<SourceExecutionScope.PublicationCountry>(target.Target.Scope);
        Assert.False(target.IsEligible);
        Assert.Contains(ScopedSourcePlanReason.UnsupportedScope, target.Reasons);
    }

    [Fact]
    public void Unconstrained_requires_explicit_unconstrained_capability()
    {
        var constrained = Plan(PublicationMarketIntent.Unconstrained,
            [new SourceScopeCapability(new SourceExecutionScope.PublicationCountry(Pt), "reference")]);
        Assert.Contains(ScopedSourcePlanReason.UnsupportedScope, Assert.Single(constrained.Targets).Reasons);

        var supported = Plan(PublicationMarketIntent.Unconstrained,
            [new SourceScopeCapability(new SourceExecutionScope.UnconstrainedPublication(), "reference")]);
        Assert.True(Assert.Single(supported.Targets).IsEligible);
    }

    [Fact]
    public void Missing_capability_fails_closed()
    {
        var plan = Plan(PublicationMarketIntent.KnownCountries([Pt]), []);
        Assert.Contains(ScopedSourcePlanReason.UnsupportedScope, Assert.Single(plan.Targets).Reasons);
    }

    [Fact]
    public void Mandatory_unknown_filter_fails_closed()
    {
        var plan = Plan(PublicationMarketIntent.KnownCountries([Pt]),
            [new SourceScopeCapability(new SourceExecutionScope.PublicationCountry(Pt), "reference")],
            filterSupport: FilterSupport.UnknownSemantics);
        var target = Assert.Single(plan.Targets);
        Assert.False(target.IsEligible);
        Assert.Contains(ScopedSourcePlanReason.MandatoryCriterionUnsupported, target.Reasons);
        Assert.Equal(CriterionDisposition.UnknownSemantics, Assert.Single(target.Criteria).Disposition);
    }

    [Fact]
    public void Operational_denials_are_reported_even_when_scope_is_supported()
    {
        var plan = Plan(PublicationMarketIntent.KnownCountries([Pt]),
            [new SourceScopeCapability(new SourceExecutionScope.PublicationCountry(Pt), "reference")],
            authorized: false, quota: CapacityAvailability.Unknown);
        var target = Assert.Single(plan.Targets);
        Assert.False(target.IsEligible);
        Assert.Contains(ScopedSourcePlanReason.NotAuthorized, target.Reasons);
        Assert.Contains(ScopedSourcePlanReason.QuotaUnavailable, target.Reasons);
    }

    [Fact]
    public void Publication_scopes_have_stable_order_independent_of_input_order()
    {
        var capabilities = new[]
        {
            new SourceScopeCapability(new SourceExecutionScope.PublicationCountry(Pt), "pt"),
            new SourceScopeCapability(new SourceExecutionScope.PublicationCountry(Gb), "gb")
        };
        var plan = Plan(PublicationMarketIntent.KnownCountries([Pt, Gb]), capabilities);
        Assert.Equal(new[] { "GB", "PT" }, plan.Targets.Select(x =>
            Assert.IsType<SourceExecutionScope.PublicationCountry>(x.Target.Scope).Country.Value));
        Assert.All(plan.Targets, x => Assert.True(x.IsEligible));
    }

    [Fact]
    public void Capability_input_mutation_does_not_change_planning()
    {
        var inputs = new List<SourceScopeCapability>
        {
            new(new SourceExecutionScope.PublicationCountry(Pt), "reference")
        };
        var scopes = new SourceScopeCapabilities(inputs);
        inputs.Clear();
        var plan = PlanWithScopes(PublicationMarketIntent.KnownCountries([Pt]), scopes);
        Assert.True(Assert.Single(plan.Targets).IsEligible);
    }

    private static ScopedDiscoveryPlan Plan(PublicationMarketIntent market,
        IEnumerable<SourceScopeCapability> capabilities, FilterSupport filterSupport = FilterSupport.SupportedByProvider,
        bool authorized = true, CapacityAvailability quota = CapacityAvailability.Available) =>
        PlanWithScopes(market, new SourceScopeCapabilities(capabilities), filterSupport, authorized, quota);

    private static ScopedDiscoveryPlan PlanWithScopes(PublicationMarketIntent market,
        SourceScopeCapabilities scopes, FilterSupport filterSupport = FilterSupport.SupportedByProvider,
        bool authorized = true, CapacityAvailability quota = CapacityAvailability.Available)
    {
        var request = new SearchRequest([Pt], [], [], [], null,
            [SearchCriterion.TargetRole], []);
        var planningRequest = new ScopedDiscoveryPlanningRequest(request,
            new DiscoveryGeographyIntent(market, []));
        var source = new SourcePlanningState(
            new SourceCapabilities(new SourceId("example"), [],
                new Dictionary<SearchCriterion, FilterSupport> { [SearchCriterion.TargetRole] = filterSupport }),
            new SourceExecutionPolicy(authorized, new SourceLicenseRights(true, false, false),
                new RateLimitPolicy(100), new SourceBudgetPolicy(1m)),
            new SourceRuntimeStatus(SourceHealth.Healthy, quota, CapacityAvailability.Available,
                [], DateTimeOffset.UnixEpoch));
        return new ScopedDiscoveryPlanner().CreatePlan(planningRequest,
            new ScopedDiscoveryPlanningSnapshot([new ScopedSourcePlanningState(source, scopes)]));
    }
}
