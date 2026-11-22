using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedDiscoveryExecutionContractsTests
{
    private static readonly CountryCode Pt = new("PT");

    [Fact]
    public void Execution_request_preserves_publication_scope_without_legacy_conversion()
    {
        var scope = new SourceExecutionScope.PublicationCountry(Pt);
        var request = new ScopedSourceExecutionRequest(
            new ScopedSourceExecutionTarget(new SourceId("provider"), scope),
            Filters(),
            [new PlannedCriterion(SearchCriterion.TargetRole, true, CriterionDisposition.AppliedByProvider)]);

        Assert.Equal(scope, request.Target.Scope);
        Assert.NotEqual(new SourceExecutionScope.LegacyMarket(Pt), request.Target.Scope);
        Assert.Equal(new SourceId("provider"), request.Target.SourceId);
    }

    [Fact]
    public void Unconstrained_provenance_does_not_invent_a_requested_market()
    {
        var scope = new SourceExecutionScope.UnconstrainedPublication();
        var provenance = new ScopedSourceProvenance(
            new SourceId("provider"), "id-1", new Uri("https://example.org/job/1"),
            scope, DateTimeOffset.UnixEpoch);
        Assert.Equal(scope, provenance.RequestedScope);
        Assert.IsType<SourceExecutionScope.UnconstrainedPublication>(provenance.RequestedScope);
    }

    [Theory]
    [InlineData(CriterionDisposition.Unsupported)]
    [InlineData(CriterionDisposition.UnknownSemantics)]
    public void Mandatory_unsupported_criteria_are_rejected(CriterionDisposition disposition)
    {
        Assert.Throws<ArgumentException>(() => new ScopedSourceExecutionRequest(Target(), Filters(),
            [new PlannedCriterion(SearchCriterion.TargetRole, true, disposition)]));
    }

    [Fact]
    public void Request_rejects_missing_or_duplicate_criteria()
    {
        Assert.Throws<ArgumentException>(() => new ScopedSourceExecutionRequest(Target(), Filters(), []));
        Assert.Throws<ArgumentException>(() => new ScopedSourceExecutionRequest(Target(), Filters(),
        [
            new PlannedCriterion(SearchCriterion.TargetRole, true, CriterionDisposition.AppliedByProvider),
            new PlannedCriterion(SearchCriterion.TargetRole, true, CriterionDisposition.AppliedPostFetch)
        ]));
        Assert.Throws<ArgumentException>(() => new ScopedSourceExecutionRequest(Target(), Filters(), [null!]));
    }

    [Fact]
    public void Request_copies_criteria_and_rejects_null_dependencies()
    {
        var criteria = new List<PlannedCriterion>
        {
            new(SearchCriterion.TargetRole, true, CriterionDisposition.AppliedPostFetch)
        };
        var request = new ScopedSourceExecutionRequest(Target(), Filters(), criteria);
        criteria.Clear();
        Assert.Single(request.Criteria);
        Assert.Throws<ArgumentNullException>(() => new ScopedSourceExecutionRequest(null!, Filters(), []));
        Assert.Throws<ArgumentNullException>(() => new ScopedSourceExecutionRequest(Target(), null!, []));
        Assert.Throws<ArgumentNullException>(() => new ScopedSourceExecutionRequest(Target(), Filters(), null!));
    }

    private static ScopedSourceExecutionTarget Target() =>
        new(new SourceId("provider"), new SourceExecutionScope.UnconstrainedPublication());

    private static SearchRequest Filters() =>
        new([Pt], [], [], [], null, [SearchCriterion.TargetRole], []);
}
