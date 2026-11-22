using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedDiscoveryPlannerGuardrailTests
{
    private static readonly CountryCode Pt = new("PT");

    [Theory]
    [InlineData(false, true, SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, false, ScopedSourcePlanReason.NotAuthorized)]
    [InlineData(true, false, SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, false, ScopedSourcePlanReason.LicenseDoesNotAllowDiscovery)]
    [InlineData(true, true, SourceHealth.Unavailable, CapacityAvailability.Available, CapacityAvailability.Available, false, ScopedSourcePlanReason.SourceUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Exhausted, CapacityAvailability.Available, false, ScopedSourcePlanReason.QuotaUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Exhausted, false, ScopedSourcePlanReason.BudgetUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, true, ScopedSourcePlanReason.TemporarilyRestricted)]
    public void Operational_guardrails_fail_closed(bool authorized, bool licensed, SourceHealth health,
        CapacityAvailability quota, CapacityAvailability budget, bool restricted, ScopedSourcePlanReason reason)
    {
        var plan = new ScopedDiscoveryPlanner().CreatePlan(Request(), Snapshot(
            authorized: authorized, licensed: licensed, health: health, quota: quota,
            budget: budget, restricted: restricted));
        var target = Assert.Single(plan.Targets);
        Assert.False(target.IsEligible);
        Assert.Contains(reason, target.Reasons);
    }

    [Fact]
    public void Preferred_unsupported_filter_does_not_deny_execution()
    {
        var request = new SearchRequest([Pt], [], [], [], null, [], [SearchCriterion.Skill]);
        var plan = new ScopedDiscoveryPlanner().CreatePlan(
            new ScopedDiscoveryPlanningRequest(request, Geography()), Snapshot());
        var target = Assert.Single(plan.Targets);
        Assert.True(target.IsEligible);
        var criterion = Assert.Single(target.Criteria);
        Assert.False(criterion.IsMandatory);
        Assert.Equal(CriterionDisposition.Unsupported, criterion.Disposition);
    }

    [Fact]
    public void Duplicate_source_ids_are_rejected()
    {
        var source = Source();
        Assert.Throws<ArgumentException>(() => new ScopedDiscoveryPlanningSnapshot(
        [
            new ScopedSourcePlanningState(source, Scopes()),
            new ScopedSourcePlanningState(source, Scopes())
        ]));
    }

    [Fact]
    public void Null_inputs_are_rejected()
    {
        var planner = new ScopedDiscoveryPlanner();
        var request = Request();
        Assert.Throws<ArgumentNullException>(() => planner.CreatePlan(null!, Snapshot()));
        Assert.Throws<ArgumentNullException>(() => planner.CreatePlan(request, null!));
        Assert.Throws<ArgumentNullException>(() => new ScopedDiscoveryPlanningRequest(null!, Geography()));
        Assert.Throws<ArgumentNullException>(() => new ScopedDiscoveryPlanningRequest(request.Filters, null!));
        Assert.Throws<ArgumentNullException>(() => new ScopedDiscoveryPlanningSnapshot(null!));
        Assert.Throws<ArgumentNullException>(() => new ScopedSourcePlanningState(null!, Scopes()));
        Assert.Throws<ArgumentNullException>(() => new ScopedSourcePlanningState(Source(), null!));
    }

    private static ScopedDiscoveryPlanningRequest Request() =>
        new(new SearchRequest([Pt], [], [], [], null, [SearchCriterion.TargetRole], []), Geography());

    private static DiscoveryGeographyIntent Geography() =>
        new(PublicationMarketIntent.KnownCountries([Pt]), []);

    private static SourceScopeCapabilities Scopes() =>
        new([new SourceScopeCapability(new SourceExecutionScope.PublicationCountry(Pt), "reference")]);

    private static ScopedDiscoveryPlanningSnapshot Snapshot(bool authorized = true, bool licensed = true,
        SourceHealth health = SourceHealth.Healthy, CapacityAvailability quota = CapacityAvailability.Available,
        CapacityAvailability budget = CapacityAvailability.Available, bool restricted = false) =>
        new([new ScopedSourcePlanningState(Source(authorized, licensed, health, quota, budget, restricted), Scopes())]);

    private static SourcePlanningState Source(bool authorized = true, bool licensed = true,
        SourceHealth health = SourceHealth.Healthy, CapacityAvailability quota = CapacityAvailability.Available,
        CapacityAvailability budget = CapacityAvailability.Available, bool restricted = false) =>
        new(
            new SourceCapabilities(new SourceId("example"), [],
                new Dictionary<SearchCriterion, FilterSupport>
                {
                    [SearchCriterion.TargetRole] = FilterSupport.SupportedByProvider
                }),
            new SourceExecutionPolicy(authorized, new SourceLicenseRights(licensed, false, false),
                new RateLimitPolicy(100), new SourceBudgetPolicy(1m)),
            new SourceRuntimeStatus(health, quota, budget,
                restricted ? ["Maintenance"] : [], DateTimeOffset.UnixEpoch));
}
