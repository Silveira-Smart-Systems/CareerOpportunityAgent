using COA.Application.Discovery;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class DiscoveryOrchestratorTests
{
    private static readonly CountryCode Gb = new("GB");
    private static readonly CountryCode Br = new("BR");

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
    public async Task Multi_market_fan_out_is_once_per_market_and_outcomes_are_sorted()
    {
        var request = new SearchRequest([Br, Gb], ["Technology"], ["Architect"], ["Remote"], null, [SearchCriterion.TargetRole], []);
        var source = new PlannedSource(new SourceId("one"), [Gb, Br], true, [SourcePlanReason.Eligible], [new PlannedCriterion(SearchCriterion.TargetRole, true, CriterionDisposition.AppliedByProvider)]);
        var adapter = new RecordingAdapter("one");
        var result = await Create(new FakeGate(), adapter).ExecuteAsync(request, new DiscoveryPlan(request, [source]), Options());
        Assert.Equal([Br, Gb], adapter.Markets.OrderBy(x => x.Value));
        Assert.Equal([Br, Gb], result.Sources.Select(x => x.Market));
    }

    [Fact]
    public async Task Adapter_failure_is_isolated_from_other_source()
    {
        var plan = new DiscoveryPlan(Request(), [Source("bad", true), Source("one", true)]);
        var result = await Create(new FakeGate(), new ThrowingAdapter("bad"), new FakeAdapter("one", Result(true))).ExecuteAsync(Request(), plan, Options());
        Assert.Equal(SourceExecutionStatus.Failed, result.Sources[0].Status);
        Assert.Equal(SourceExecutionStatus.Succeeded, result.Sources[1].Status);
    }

    [Fact]
    public async Task Non_cooperative_timeout_returns_before_adapter_completes()
    {
        var time = new FakeTimeProvider();
        var adapter = new BlockingAdapter("one");
        var execution = new DiscoveryOrchestrator(new Resolver([adapter]), new FakeGate(), time)
            .ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), new DiscoveryExecutionOptions(1, TimeSpan.FromMinutes(1)));
        await adapter.Started.Task;
        time.Advance(TimeSpan.FromMinutes(1));
        var outcome = await execution;
        Assert.Equal(SourceExecutionStatus.TimedOut, Assert.Single(outcome.Sources).Status);
        Assert.False(adapter.Completion.Task.IsCompleted);
        adapter.Completion.TrySetResult(true);
        await adapter.Finished.Task;
    }

    [Fact]
    public async Task Timed_out_operation_retains_the_only_slot_until_its_real_task_completes()
    {
        var time = new FakeTimeProvider();
        var first = new BlockingAdapter("a");
        var second = new BlockingAdapter("b");
        var request = Request();
        var plan = new DiscoveryPlan(request, [Source("a", true), Source("b", true)]);
        var execution = new DiscoveryOrchestrator(new Resolver([first, second]), new FakeGate(), time)
            .ExecuteAsync(request, plan, new DiscoveryExecutionOptions(1, TimeSpan.FromMinutes(1)));

        await first.Started.Task;
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.False(second.Started.Task.IsCompleted);

        first.Completion.TrySetResult(true);
        await second.Started.Task;
        second.Completion.TrySetResult(true);
        await first.Finished.Task;
        await second.Finished.Task;

        var outcomes = await execution;
        Assert.Equal(SourceExecutionStatus.TimedOut, outcomes.Sources.Single(x => x.SourceId == new SourceId("a")).Status);
        Assert.Equal(SourceExecutionStatus.Succeeded, outcomes.Sources.Single(x => x.SourceId == new SourceId("b")).Status);
    }

    [Fact]
    public async Task Caller_cancellation_after_start_consumes_the_permit()
    {
        using var cancellation = new CancellationTokenSource();
        var gate = new FakeGate(); var adapter = new BlockingAdapter("one");
        var execution = Create(gate, adapter).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), Options(), cancellation.Token);
        await adapter.Started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(1, gate.Permit.Started); Assert.Equal(1, gate.Permit.Consumed); Assert.Equal(0, gate.Permit.Released);
        adapter.Completion.TrySetResult(true);
        await adapter.Finished.Task;
    }

    [Fact]
    public async Task Consume_failure_is_reported_as_lifecycle_failure()
    {
        var gate = new FakeGate(permit: new FakePermit(new SourceId("one"), throwConsume: true));
        var outcome = await Create(gate, new FakeAdapter("one", Result(true))).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), Options());
        var source = Assert.Single(outcome.Sources);
        Assert.Equal(SourceExecutionStatus.Failed, source.Status);
        Assert.Contains(source.Diagnostics, diagnostic => diagnostic.Code == "PermitLifecycleFailed");
    }

    [Fact]
    public async Task Release_failure_does_not_replace_caller_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var gate = new FakeGate(permit: new FakePermit(new SourceId("one"), throwRelease: true), afterReserve: cancellation.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(gate, new FakeAdapter("one", Result(true))).ExecuteAsync(Request(), new DiscoveryPlan(Request(), [Source("one", true)]), Options(), cancellation.Token));
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
    private sealed class ThrowingAdapter(string id) : IOpportunitySourceAdapter
    { public SourceId SourceId { get; } = new(id); public Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken ct) => throw new InvalidOperationException(); }
    private sealed class RecordingAdapter(string id) : IOpportunitySourceAdapter
    { public SourceId SourceId { get; } = new(id); public List<CountryCode> Markets { get; } = []; public Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken ct) { Markets.Add(target.Market); return Task.FromResult(Result(true)); } }
    private sealed class BlockingAdapter(string id) : IOpportunitySourceAdapter
    { public SourceId SourceId { get; } = new(id); public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource<bool> Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public async Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken ct) { Started.TrySetResult(true); try { await Completion.Task; return new SourceSearchResult([new SourceOpportunityCandidate(new SourceProvenance(SourceId, "1", new Uri("https://example.test/1"), target.Market, DateTimeOffset.UnixEpoch), new Dictionary<string, SourceObservedValue>())], [new AppliedCriterion(SearchCriterion.TargetRole, CriterionApplication.AppliedByProvider)]); } finally { Finished.TrySetResult(true); } } }
    private sealed class FakeGate(SourceExecutionAuthorization? result = null, FakePermit? permit = null, Action? afterReserve = null) : ISourceExecutionGate
    { public int Calls { get; private set; } public FakePermit Permit { get; } = permit ?? new(new SourceId("one")); public Task<SourceExecutionAuthorization> AuthorizeAndReserveAsync(SourceExecutionTarget target, CancellationToken ct) { Calls++; afterReserve?.Invoke(); return Task.FromResult(result ?? new SourceExecutionAuthorization.Granted(Permit)); } }
    private sealed class FakePermit(SourceId source, bool throwConsume = false, bool throwRelease = false) : SourceExecutionPermit
    { public override SourceId SourceId => source; public int Started; public int Consumed; public int Released; public override Task MarkAttemptStartedAsync(CancellationToken ct) { Started++; return Task.CompletedTask; } public override Task ConsumeAsync(CancellationToken ct) { Consumed++; if (throwConsume) throw new InvalidOperationException(); return Task.CompletedTask; } public override Task ReleaseAsync(CancellationToken ct) { Released++; if (throwRelease) throw new InvalidOperationException(); return Task.CompletedTask; } }
}
