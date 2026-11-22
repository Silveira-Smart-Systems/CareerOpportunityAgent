using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedDiscoveryCancellationRaceTests
{
    [Fact]
    public async Task Caller_cancellation_keeps_slot_until_noncooperative_adapter_completes()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstFinished = new TaskCompletionSource<ScopedSourceSearchResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var adapter = new Adapter((_, _) =>
        {
            Interlocked.Increment(ref calls);
            firstStarted.TrySetResult();
            return firstFinished.Task; // deliberately ignores cancellation
        });
        using var cancellation = new CancellationTokenSource();
        var orchestrator = new ScopedDiscoveryOrchestrator(new Resolver(adapter), new Gate());
        var execution = orchestrator.ExecuteAsync(Request(), Plan(),
            new DiscoveryExecutionOptions(1, TimeSpan.FromSeconds(10)), cancellation.Token);
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            Assert.Equal(1, Volatile.Read(ref calls));
        }
        finally
        {
            firstFinished.TrySetResult(new ScopedSourceSearchResult([], []));
        }
    }

    private static ScopedDiscoveryPlanningRequest Request() =>
        new(new SearchRequest([new CountryCode("PT")], [], [], [], null, [], []),
            new DiscoveryGeographyIntent(PublicationMarketIntent.Unconstrained, []));
    private static ScopedDiscoveryPlan Plan() =>
        new([new ScopedPlannedTarget(
            new ScopedSourceExecutionTarget(new SourceId("provider"),
                new SourceExecutionScope.UnconstrainedPublication()),
            [ScopedSourcePlanReason.Eligible], [])]);
    private sealed class Resolver(IScopedOpportunitySourceAdapter adapter) : IScopedOpportunitySourceAdapterResolver
    {
        public IScopedOpportunitySourceAdapter? Find(SourceId sourceId) => adapter;
    }
    private sealed class Adapter(
        Func<ScopedSourceExecutionRequest, CancellationToken, Task<ScopedSourceSearchResult>> search)
        : IScopedOpportunitySourceAdapter
    {
        public SourceId SourceId => new("provider");
        public Task<ScopedSourceSearchResult> SearchAsync(
            ScopedSourceExecutionRequest request, CancellationToken cancellationToken) =>
            search(request, cancellationToken);
    }
    private sealed class Gate : IScopedSourceExecutionGate
    {
        public Task<ScopedSourceExecutionAuthorization> AuthorizeAndReserveAsync(
            ScopedSourceExecutionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<ScopedSourceExecutionAuthorization>(
                new ScopedSourceExecutionAuthorization.Granted(new Permit(request.Target)));
    }
    private sealed class Permit(ScopedSourceExecutionTarget target) : ScopedSourceExecutionPermit
    {
        public override ScopedSourceExecutionTarget Target => target;
        public override Task MarkAttemptStartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task ConsumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
