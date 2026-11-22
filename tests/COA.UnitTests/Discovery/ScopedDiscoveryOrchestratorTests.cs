using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedDiscoveryOrchestratorTests
{
    [Fact]
    public async Task Blocked_gate_never_calls_adapter()
    {
        var adapter = new StubAdapter((_, _) => throw new Exception("Adapter must not run"));
        var gate = new StubGate((_, _) => Task.FromResult<ScopedSourceExecutionAuthorization>(
            new ScopedSourceExecutionAuthorization.Blocked(ScopedSourceExecutionBlockReason.NotAuthorized)));
        var result = await Orchestrator(adapter, gate).ExecuteAsync(Request(), Plan(), Options());
        Assert.Equal(SourceExecutionStatus.Blocked, Assert.Single(result.Sources).Status);
        Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public async Task Valid_scoped_result_is_accepted_and_permit_consumed()
    {
        var permit = new StubPermit(Target());
        var adapter = new StubAdapter((request, _) => Task.FromResult(
            new ScopedSourceSearchResult(
                [new ScopedSourceOpportunityCandidate(
                    new ScopedSourceProvenance(request.Target.SourceId, "external",
                        new Uri("https://example.org/job/1"), request.Target.Scope, DateTimeOffset.UtcNow),
                    new Dictionary<string, SourceObservedValue>())], [])));
        var result = await Orchestrator(adapter, Granted(permit)).ExecuteAsync(Request(), Plan(), Options());
        Assert.Equal(SourceExecutionStatus.Succeeded, Assert.Single(result.Sources).Status);
        Assert.Equal(1, permit.Started);
        Assert.Equal(1, permit.Consumed);
        Assert.Equal(0, permit.Released);
    }

    [Fact]
    public async Task Unconstrained_scope_preserves_observed_country_without_conflict_diagnostic()
    {
        var permit = new StubPermit(Target());
        var claim = new ScopedSourceGeographyClaim(
            new ObservedPublicationGeography.Countries([new CountryCode("ES")]));
        var adapter = new StubAdapter((request, _) => Task.FromResult(
            new ScopedSourceSearchResult(
                [new ScopedSourceOpportunityCandidate(
                    new ScopedSourceProvenance(request.Target.SourceId, "external",
                        new Uri("https://example.org/job/1"), request.Target.Scope, DateTimeOffset.UtcNow)
                    { GeographyClaim = claim },
                    new Dictionary<string, SourceObservedValue>())], [])));
        var outcome = Assert.Single((await Orchestrator(adapter, Granted(permit))
            .ExecuteAsync(Request(), Plan(), Options())).Sources);
        Assert.Equal(SourceExecutionStatus.Succeeded, outcome.Status);
        Assert.Empty(outcome.Diagnostics);
        Assert.Same(claim, Assert.Single(outcome.Candidates).Provenance.GeographyClaim);
        Assert.Equal(1, permit.Consumed);
    }

    [Fact]
    public async Task Wrong_scope_provenance_is_rejected()
    {
        var permit = new StubPermit(Target());
        var adapter = new StubAdapter((request, _) => Task.FromResult(
            new ScopedSourceSearchResult(
                [new ScopedSourceOpportunityCandidate(
                    new ScopedSourceProvenance(request.Target.SourceId, "external",
                        new Uri("https://example.org/job/1"),
                        new SourceExecutionScope.LegacyMarket(new CountryCode("PT")),
                        DateTimeOffset.UtcNow),
                    new Dictionary<string, SourceObservedValue>())], [])));
        var result = await Orchestrator(adapter, Granted(permit)).ExecuteAsync(Request(), Plan(), Options());
        Assert.Equal(SourceExecutionStatus.InvalidPlan, Assert.Single(result.Sources).Status);
        Assert.Equal(1, permit.Consumed);
    }

    [Fact]
    public async Task Timeout_does_not_release_started_permit()
    {
        var permit = new StubPermit(Target());
        var pending = new TaskCompletionSource<ScopedSourceSearchResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var adapter = new StubAdapter((_, _) => pending.Task);
        var result = await Orchestrator(adapter, Granted(permit)).ExecuteAsync(
            Request(), Plan(), new DiscoveryExecutionOptions(1, TimeSpan.FromMilliseconds(30)));
        Assert.Equal(SourceExecutionStatus.TimedOut, Assert.Single(result.Sources).Status);
        Assert.Equal(1, permit.Consumed);
        Assert.Equal(0, permit.Released);
        pending.SetResult(new ScopedSourceSearchResult([], []));
    }

    [Fact]
    public async Task Cancelled_caller_does_not_start_adapter()
    {
        var adapter = new StubAdapter((_, _) => throw new Exception("Adapter must not run"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Orchestrator(adapter, Granted(new StubPermit(Target()))).ExecuteAsync(
                Request(), Plan(), Options(), cts.Token));
        Assert.Equal(0, adapter.Calls);
    }

    private static DiscoveryExecutionOptions Options() => new(2, TimeSpan.FromSeconds(2));
    private static ScopedDiscoveryPlanningRequest Request() =>
        new(new SearchRequest([new CountryCode("PT")], [], [], [], null, [], []),
            new DiscoveryGeographyIntent(PublicationMarketIntent.Unconstrained, []));
    private static ScopedSourceExecutionTarget Target() =>
        new(new SourceId("provider"), new SourceExecutionScope.UnconstrainedPublication());
    private static ScopedDiscoveryPlan Plan() =>
        new([new ScopedPlannedTarget(Target(), [ScopedSourcePlanReason.Eligible], [])]);
    private static ScopedDiscoveryOrchestrator Orchestrator(
        IScopedOpportunitySourceAdapter adapter, IScopedSourceExecutionGate gate) =>
        new(new StubResolver(adapter), gate);
    private static IScopedSourceExecutionGate Granted(StubPermit permit) =>
        new StubGate((_, _) => Task.FromResult<ScopedSourceExecutionAuthorization>(
            new ScopedSourceExecutionAuthorization.Granted(permit)));

    private sealed class StubResolver(IScopedOpportunitySourceAdapter adapter)
        : IScopedOpportunitySourceAdapterResolver
    {
        public IScopedOpportunitySourceAdapter? Find(SourceId sourceId) =>
            sourceId == adapter.SourceId ? adapter : null;
    }
    private sealed class StubAdapter(
        Func<ScopedSourceExecutionRequest, CancellationToken, Task<ScopedSourceSearchResult>> search)
        : IScopedOpportunitySourceAdapter
    {
        public SourceId SourceId => new("provider");
        public int Calls { get; private set; }
        public Task<ScopedSourceSearchResult> SearchAsync(
            ScopedSourceExecutionRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return search(request, cancellationToken);
        }
    }
    private sealed class StubGate(
        Func<ScopedSourceExecutionRequest, CancellationToken, Task<ScopedSourceExecutionAuthorization>> authorize)
        : IScopedSourceExecutionGate
    {
        public Task<ScopedSourceExecutionAuthorization> AuthorizeAndReserveAsync(
            ScopedSourceExecutionRequest request, CancellationToken cancellationToken) =>
            authorize(request, cancellationToken);
    }
    private sealed class StubPermit(ScopedSourceExecutionTarget target) : ScopedSourceExecutionPermit
    {
        public int Started { get; private set; }
        public int Consumed { get; private set; }
        public int Released { get; private set; }
        public override ScopedSourceExecutionTarget Target => target;
        public override Task MarkAttemptStartedAsync(CancellationToken cancellationToken)
        { Started++; return Task.CompletedTask; }
        public override Task ConsumeAsync(CancellationToken cancellationToken)
        { Consumed++; return Task.CompletedTask; }
        public override Task ReleaseAsync(CancellationToken cancellationToken)
        { Released++; return Task.CompletedTask; }
    }
}
