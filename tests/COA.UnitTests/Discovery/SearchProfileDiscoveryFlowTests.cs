using COA.Application.Discovery;
using COA.Application.SearchProfiles;
using COA.Domain.SearchProfiles;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class SearchProfileDiscoveryFlowTests
{
    [Fact]
    public async Task Mandatory_remote_country_with_unknown_semantics_never_reaches_gate_or_adapter()
    {
        var request = Assert.IsType<MappedSearchRequest>(new JobSearchProfileSearchRequestMapper().Map(Profile(
            [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole, SearchProfileCriterion.RemoteFromCountry]))).SearchRequest;
        var source = new SourcePlanningState(
            new SourceCapabilities(new SourceId("fake"), [new CountryCode("GB")],
            [new KeyValuePair<SearchCriterion, FilterSupport>(SearchCriterion.Category, FilterSupport.SupportedByProvider),
                new(SearchCriterion.TargetRole, FilterSupport.SupportedByProvider),
                new(SearchCriterion.RemoteFromCountry, FilterSupport.UnknownSemantics)]),
            Policy(), Runtime());
        var plan = new DiscoveryPlanner().CreatePlan(request, new DiscoveryPlanningSnapshot([source]));
        var gate = new CountingGate(); var adapter = new CountingAdapter();

        var outcome = await new DiscoveryOrchestrator(new Resolver(adapter), gate)
            .ExecuteAsync(request, plan, new DiscoveryExecutionOptions(1, TimeSpan.FromSeconds(1)));

        Assert.Empty(plan.EligibleSources); Assert.Empty(outcome.Sources);
        Assert.Equal(0, gate.Calls); Assert.Equal(0, adapter.Calls);
        Assert.Contains(SourcePlanReason.MandatoryCriterionUnsupported, Assert.Single(plan.Sources).Reasons);
    }

    private static JobSearchProfile Profile(IReadOnlyList<SearchProfileCriterion> mandatory) =>
        JobSearchProfile.Create(Guid.NewGuid(), "Remote", new JobSearchProfilePreferences(
            ["Technology"], ["Architect"], [], ["GB"], "PT", [], null, [], [], [], mandatory, []), DateTime.UtcNow);

    private static SourceExecutionPolicy Policy() => new(true, new SourceLicenseRights(true, false, false), new RateLimitPolicy(1), new SourceBudgetPolicy(0m));
    private static SourceRuntimeStatus Runtime() => new(SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, [], DateTimeOffset.UnixEpoch);

    private sealed class Resolver(IOpportunitySourceAdapter adapter) : IOpportunitySourceAdapterResolver
    {
        public IOpportunitySourceAdapter? Find(SourceId sourceId) => sourceId == adapter.SourceId ? adapter : null;
    }

    private sealed class CountingAdapter : IOpportunitySourceAdapter
    {
        public SourceId SourceId { get; } = new("fake");
        public int Calls { get; private set; }
        public Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new SourceSearchResult([], []));
        }
    }

    private sealed class CountingGate : ISourceExecutionGate
    {
        public int Calls { get; private set; }
        public Task<SourceExecutionAuthorization> AuthorizeAndReserveAsync(SourceExecutionTarget target, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("The gate must not be called for an ineligible source.");
        }
    }
}
