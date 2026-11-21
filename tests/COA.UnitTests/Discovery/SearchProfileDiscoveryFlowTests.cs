using COA.Application.Discovery;
using COA.Application.SearchProfiles;
using COA.Domain.SearchProfiles;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class SearchProfileDiscoveryFlowTests
{
    private static readonly CountryCode Pt = new("PT");
    private static readonly CountryCode Gb = new("GB");
    private static readonly CountryCode Br = new("BR");

    [Fact]
    public async Task Portugal_only_executes_only_the_portugal_fake_pair()
    {
        var (outcome, gate, adapters, _) = await Execute(Profile(["PT"]), [Source("gb-fake", [Gb]), Source("pt-fake", [Pt])]);

        Assert.Equal([(new SourceId("pt-fake"), Pt)], gate.Targets);
        Assert.Equal([(new SourceId("pt-fake"), Pt)], adapters.Targets);
        Assert.Single(outcome.Sources);
    }

    [Fact]
    public async Task Great_britain_only_executes_only_the_great_britain_fake_pair()
    {
        var (_, gate, adapters, _) = await Execute(Profile(["GB"]), [Source("gb-fake", [Gb]), Source("pt-fake", [Pt])]);

        Assert.Equal([(new SourceId("gb-fake"), Gb)], gate.Targets);
        Assert.Equal([(new SourceId("gb-fake"), Gb)], adapters.Targets);
    }

    [Fact]
    public async Task Portugal_brazil_and_great_britain_fan_out_only_to_supported_pairs()
    {
        var (_, gate, adapters, _) = await Execute(Profile(["PT", "BR", "GB"]), [Source("west-fake", [Pt, Br]), Source("uk-fake", [Gb])]);

        var expected = new[] { (new SourceId("uk-fake"), Gb), (new SourceId("west-fake"), Br), (new SourceId("west-fake"), Pt) };
        Assert.Equal(expected, gate.Targets.OrderBy(target => target.Item1.Value).ThenBy(target => target.Item2.Value));
        Assert.Equal(expected, adapters.Targets.OrderBy(target => target.Item1.Value).ThenBy(target => target.Item2.Value));
    }

    [Fact]
    public async Task No_eligible_source_makes_no_gate_or_adapter_call()
    {
        var (outcome, gate, adapters, _) = await Execute(Profile(["PT"]), [Source("gb-fake", [Gb])]);

        Assert.Empty(outcome.Sources); Assert.Empty(gate.Targets); Assert.Empty(adapters.Targets);
    }

    [Fact]
    public async Task Unauthorized_or_unlicensed_sources_are_not_executed()
    {
        var (_, gate, adapters, _) = await Execute(Profile(["GB"]), [Source("unauthorized-fake", [Gb], authorized: false), Source("unlicensed-fake", [Gb], licensed: false)]);

        Assert.Empty(gate.Targets); Assert.Empty(adapters.Targets);
    }

    [Fact]
    public async Task Mandatory_remote_country_with_unknown_semantics_never_reaches_gate_or_adapter()
    {
        var profile = JobSearchProfile.Create(Guid.NewGuid(), "Remote", new JobSearchProfilePreferences(
            ["Technology"], ["Architect"], [], ["GB"], "PT", [], null, [], [], [],
            [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole, SearchProfileCriterion.RemoteFromCountry], []), DateTime.UtcNow);
        var source = Source("fake", [Gb], filters: new Dictionary<SearchCriterion, FilterSupport>
        {
            [SearchCriterion.Category] = FilterSupport.SupportedByProvider,
            [SearchCriterion.TargetRole] = FilterSupport.SupportedByProvider,
            [SearchCriterion.RemoteFromCountry] = FilterSupport.UnknownSemantics
        });

        var (outcome, gate, adapters, plan) = await Execute(profile, [source]);

        Assert.Empty(plan.EligibleSources); Assert.Empty(outcome.Sources); Assert.Empty(gate.Targets); Assert.Empty(adapters.Targets);
        Assert.Contains(SourcePlanReason.MandatoryCriterionUnsupported, Assert.Single(plan.Sources).Reasons);
    }

    private static async Task<(DiscoveryOutcome Outcome, RecordingGate Gate, RecordingResolver Adapters, DiscoveryPlan Plan)> Execute(JobSearchProfile profile, SourcePlanningState[] sources)
    {
        var request = Assert.IsType<MappedSearchRequest>(new JobSearchProfileSearchRequestMapper().Map(profile)).SearchRequest;
        var plan = new DiscoveryPlanner().CreatePlan(request, new DiscoveryPlanningSnapshot(sources));
        var gate = new RecordingGate(); var adapters = new RecordingResolver(sources.Select(source => source.Capabilities.SourceId));
        var outcome = await new DiscoveryOrchestrator(adapters, gate).ExecuteAsync(request, plan, new DiscoveryExecutionOptions(4, TimeSpan.FromSeconds(1)));
        return (outcome, gate, adapters, plan);
    }

    private static JobSearchProfile Profile(IReadOnlyList<string> markets) => JobSearchProfile.Create(Guid.NewGuid(), "Search", new JobSearchProfilePreferences(
        ["Technology"], ["Architect"], [], markets, null, [], null, [], [], [],
        [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole], []), DateTime.UtcNow);

    private static SourcePlanningState Source(string id, IReadOnlyList<CountryCode> markets, bool authorized = true, bool licensed = true,
        IReadOnlyDictionary<SearchCriterion, FilterSupport>? filters = null) => new(
            new SourceCapabilities(new SourceId(id), markets, filters ?? new Dictionary<SearchCriterion, FilterSupport>
            {
                [SearchCriterion.Category] = FilterSupport.SupportedByProvider,
                [SearchCriterion.TargetRole] = FilterSupport.SupportedByProvider
            }),
            new SourceExecutionPolicy(authorized, new SourceLicenseRights(licensed, false, false), new RateLimitPolicy(10), new SourceBudgetPolicy(0m)),
            new SourceRuntimeStatus(SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, [], DateTimeOffset.UnixEpoch));

    private sealed class RecordingResolver(IEnumerable<SourceId> sourceIds) : IOpportunitySourceAdapterResolver
    {
        private readonly IReadOnlyDictionary<SourceId, RecordingAdapter> _adapters = sourceIds.ToDictionary(id => id, id => new RecordingAdapter(id));
        public IReadOnlyList<(SourceId, CountryCode)> Targets => _adapters.Values.SelectMany(adapter => adapter.Targets).ToArray();
        public IOpportunitySourceAdapter? Find(SourceId sourceId) => _adapters.GetValueOrDefault(sourceId);
    }

    private sealed class RecordingAdapter(SourceId sourceId) : IOpportunitySourceAdapter
    {
        public SourceId SourceId { get; } = sourceId;
        public List<(SourceId, CountryCode)> Targets { get; } = [];
        public Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken cancellationToken)
        {
            Targets.Add((target.SourceId, target.Market));
            var applied = target.Criteria.Where(criterion => criterion.IsMandatory)
                .Select(criterion => new AppliedCriterion(criterion.Criterion, CriterionApplication.AppliedByProvider)).ToArray();
            return Task.FromResult(new SourceSearchResult([], applied));
        }
    }

    private sealed class RecordingGate : ISourceExecutionGate
    {
        public List<(SourceId, CountryCode)> Targets { get; } = [];
        public Task<SourceExecutionAuthorization> AuthorizeAndReserveAsync(SourceExecutionTarget target, CancellationToken cancellationToken)
        {
            Targets.Add((target.SourceId, target.Market));
            return Task.FromResult<SourceExecutionAuthorization>(new SourceExecutionAuthorization.Granted(new Permit(target.SourceId)));
        }
    }

    private sealed class Permit(SourceId sourceId) : SourceExecutionPermit
    {
        public override SourceId SourceId => sourceId;
        public override Task MarkAttemptStartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task ConsumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
