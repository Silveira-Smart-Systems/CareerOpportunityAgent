using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedDiscoveryExecutionPlanValidatorTests
{
    private static readonly CountryCode Pt = new("PT");
    private static readonly CountryCode Gb = new("GB");

    [Fact]
    public void Planner_generated_plan_is_accepted()
    {
        var request = Request();
        ScopedDiscoveryExecutionPlanValidator.Validate(request, new ScopedDiscoveryPlanner().CreatePlan(
            request, new ScopedDiscoveryPlanningSnapshot([])));
    }

    [Fact]
    public void Legacy_market_cannot_be_used_for_publication_country()
    {
        AssertInvalid(new ScopedPlannedTarget(Target(new SourceExecutionScope.LegacyMarket(Pt)),
            [ScopedSourcePlanReason.Eligible], Criteria()));
    }

    [Fact]
    public void Different_publication_country_cannot_widen_the_request()
    {
        AssertInvalid(new ScopedPlannedTarget(Target(new SourceExecutionScope.PublicationCountry(Gb)),
            [ScopedSourcePlanReason.Eligible], Criteria()));
    }

    [Fact]
    public void Unconstrained_scope_cannot_replace_known_publication_country()
    {
        AssertInvalid(new ScopedPlannedTarget(Target(new SourceExecutionScope.UnconstrainedPublication()),
            [ScopedSourcePlanReason.Eligible], Criteria()));
    }

    [Fact]
    public void Duplicate_targets_are_rejected()
    {
        var item = ValidTarget();
        Assert.Throws<ArgumentException>(() => ScopedDiscoveryExecutionPlanValidator.Validate(
            Request(), new ScopedDiscoveryPlan([item, item])));
    }

    [Fact]
    public void Eligible_target_with_unknown_mandatory_criterion_is_rejected()
    {
        AssertInvalid(new ScopedPlannedTarget(Target(new SourceExecutionScope.PublicationCountry(Pt)),
            [ScopedSourcePlanReason.Eligible],
            [new PlannedCriterion(SearchCriterion.TargetRole, true, CriterionDisposition.UnknownSemantics)]));
    }

    [Fact]
    public void Inconsistent_eligibility_reasons_are_rejected()
    {
        AssertInvalid(new ScopedPlannedTarget(Target(new SourceExecutionScope.PublicationCountry(Pt)),
            [ScopedSourcePlanReason.Eligible, ScopedSourcePlanReason.NotAuthorized], Criteria()));
    }

    [Fact]
    public void Missing_mandatory_criterion_is_rejected()
    {
        AssertInvalid(new ScopedPlannedTarget(Target(new SourceExecutionScope.PublicationCountry(Pt)),
            [ScopedSourcePlanReason.Eligible], []));
    }

    [Fact]
    public void Valid_eligible_target_is_accepted()
    {
        ScopedDiscoveryExecutionPlanValidator.Validate(Request(), new ScopedDiscoveryPlan([ValidTarget()]));
    }

    private static void AssertInvalid(ScopedPlannedTarget target) =>
        Assert.Throws<ArgumentException>(() => ScopedDiscoveryExecutionPlanValidator.Validate(
            Request(), new ScopedDiscoveryPlan([target])));

    private static ScopedPlannedTarget ValidTarget() =>
        new(Target(new SourceExecutionScope.PublicationCountry(Pt)),
            [ScopedSourcePlanReason.Eligible], Criteria());

    private static ScopedSourceExecutionTarget Target(SourceExecutionScope scope) =>
        new(new SourceId("provider"), scope);

    private static PlannedCriterion[] Criteria() =>
        [new(SearchCriterion.TargetRole, true, CriterionDisposition.AppliedByProvider)];

    private static ScopedDiscoveryPlanningRequest Request() =>
        new(new SearchRequest([Pt], [], [], [], null, [SearchCriterion.TargetRole], []),
            new DiscoveryGeographyIntent(PublicationMarketIntent.KnownCountries([Pt]), []));
}
