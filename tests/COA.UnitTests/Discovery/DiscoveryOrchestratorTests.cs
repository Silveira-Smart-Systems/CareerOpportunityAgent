using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class DiscoveryOrchestratorTests
{
    private static readonly CountryCode Gb = new("GB");

    [Fact]
    public async Task Zero_sources_makes_no_adapter_or_gate_call()
    {
        var gate = new FakeGate();
        var result = await Create(gate).ExecuteAsync(Request(), new DiscoveryPlan(Request(), []), Options());
        Assert.Empty(result.Sources); Assert.Equal(0, gate.Calls);
    }

    [Fact]
    public async Task Mismatched_plan_is_rejected_before_execution()
    {
        var gate = new FakeGate();
        var plan = new DiscoveryPlan(Request(), [Source("fake", true)]);
        await Assert.ThrowsAsync<ArgumentException>(() => Create(gate).ExecuteAsync(Request(role: "Other"), plan, Options()));
        Assert.Equal(0, gate.Calls);
    }

    [Fact]
    public async Task Missing_adapter_and_successful_source_are_isolated()
    {
        var gate = new FakeGate();
        var adapter = new FakeAdapter("one", Result(applied: true));
        var outcome = await Create(gate, adapter).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("missing", true), Source("one", true)]), Options());
        Assert.Equal(SourceExecutionStatus.AdapterMissing, outcome.Sources[0].Status);
        Assert.Equal(SourceExecutionStatus.Succeeded, outcome.Sources[1].Status);
        Assert.Single(outcome.Candidates); Assert.Equal(1, adapter.Calls);
    }

    [Fact]
    public async Task Gate_revalidation_blocks_without_calling_adapter()
    {
        var gate = new FakeGate(new SourceExecutionAuthorization.Blocked(SourceExecutionBlockReason.QuotaUnavailable));
        var adapter = new FakeAdapter("one", Result(true));
        var outcome = await Create(gate, adapter).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), Options());
        Assert.Equal(SourceExecutionStatus.Blocked, Assert.Single(outcome.Sources).Status); Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public async Task Manipulated_market_or_missing_mandatory_criterion_fails_closed_before_gate()
    {
        var gate = new FakeGate();
        var invalid = new PlannedSource(new SourceId("one"), [new CountryCode("PT")], true, [SourcePlanReason.Eligible], []);
        await Assert.ThrowsAsync<ArgumentException>(() => Create(gate, new FakeAdapter("one", Result(true))).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [invalid]), Options()));
        Assert.Equal(0, gate.Calls);
    }

    [Fact]
    public async Task Wrong_provenance_rejects_candidates()
    {
        var wrong = new SourceSearchResult([new SourceOpportunityCandidate(new SourceProvenance(new SourceId("other"), "1", new Uri("https://example.test/1"), Gb, DateTimeOffset.UnixEpoch), new Dictionary<string, SourceObservedValue>())], [new AppliedCriterion(SearchCriterion.TargetRole, CriterionApplication.AppliedByProvider)]);
        var outcome = await Create(new FakeGate(), new FakeAdapter("one", wrong)).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), Options());
        Assert.Equal(SourceExecutionStatus.InvalidPlan, Assert.Single(outcome.Sources).Status); Assert.Empty(outcome.Candidates);
    }

    [Fact]
    public async Task Unapplied_mandatory_criterion_rejects_all_candidates_and_consumes_attempt()
    {
        var gate = new FakeGate(); var adapter = new FakeAdapter("one", Result(false));
        var outcome = await Create(gate, adapter).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), Options());
        Assert.Equal(SourceExecutionStatus.InvalidPlan, Assert.Single(outcome.Sources).Status);
        Assert.Empty(outcome.Candidates); Assert.Equal(1, gate.Permit.Started); Assert.Equal(1, gate.Permit.Consumed); Assert.Equal(0, gate.Permit.Released);
    }

    [Fact]
    public async Task Cancellation_before_start_releases_reservation()
    {
        using var cancelled = new CancellationTokenSource();
        var gate = new FakeGate(afterReserve: cancelled.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(gate, new FakeAdapter("one", Result(true))).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), Options(), cancelled.Token));
        Assert.Equal(1, gate.Calls); Assert.Equal(0, gate.Permit.Started); Assert.Equal(0, gate.Permit.Consumed); Assert.Equal(1, gate.Permit.Released);
    }

    private static DiscoveryOrchestrator Create(FakeGate gate, params IOpportunitySourceAdapter[] adapters) => new(new Resolver(adapters), gate);
    private static DiscoveryExecutionOptions Options() => new(2, TimeSpan.FromSeconds(1));
    private static SearchRequest Request(string role = "Architect") => new([Gb], ["Technology"], [role], ["Remote"], null, [SearchCriterion.TargetRole], []);
    private static PlannedSource Source(string id, bool eligible) => new(new SourceId(id), [Gb], eligible,
        eligible ? [SourcePlanReason.Eligible] : [SourcePlanReason.MandatoryCriterionUnsupported],
        [new PlannedCriterion(SearchCriterion.TargetRole, true, CriterionDisposition.AppliedByProvider)]);
    private static SourceSearchResult Result(bool applied) => new(
        [new SourceOpportunityCandidate(new SourceProvenance(new SourceId("one"), "1", new Uri("https://example.test/1"), Gb, DateTimeOffset.UnixEpoch), new Dictionary<string, SourceObservedValue>())],
        applied ? [new AppliedCriterion(SearchCriterion.TargetRole, CriterionApplication.AppliedByProvider)] : []);

    private sealed class Resolver(IEnumerable<IOpportunitySourceAdapter> adapters) : IOpportunitySourceAdapterResolver
    { private readonly Dictionary<SourceId, IOpportunitySourceAdapter> _items = adapters.ToDictionary(x => x.SourceId); public IOpportunitySourceAdapter? Find(SourceId id) => _items.GetValueOrDefault(id); }
    private sealed class FakeAdapter(string id, SourceSearchResult result) : IOpportunitySourceAdapter
    { public SourceId SourceId { get; } = new(id); public int Calls { get; private set; } public Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken ct) { Calls++; return Task.FromResult(result); } }
    private sealed class FakeGate(SourceExecutionAuthorization? result = null, Action? afterReserve = null) : ISourceExecutionGate
    { public int Calls { get; private set; } public FakePermit Permit { get; } = new(new SourceId("one")); public Task<SourceExecutionAuthorization> AuthorizeAndReserveAsync(SourceExecutionTarget target, CancellationToken ct) { Calls++; afterReserve?.Invoke(); return Task.FromResult(result ?? new SourceExecutionAuthorization.Granted(Permit)); } }
    private sealed class FakePermit(SourceId source) : SourceExecutionPermit
    { public override SourceId SourceId => source; public int Started; public int Consumed; public int Released; public override Task MarkAttemptStartedAsync(CancellationToken ct) { Started++; return Task.CompletedTask; } public override Task ConsumeAsync(CancellationToken ct) { Consumed++; return Task.CompletedTask; } public override Task ReleaseAsync(CancellationToken ct) { Released++; return Task.CompletedTask; } }
}
