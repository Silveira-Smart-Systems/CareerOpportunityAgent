using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedDiscoveryConcurrencyTests
{
    [Fact]
    public async Task Timed_out_adapter_retains_concurrency_slot_until_late_operation_finishes()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCompletion = new TaskCompletionSource<ScopedSourceSearchResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var adapter = new ControlledAdapter((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstStarted.TrySetResult();
                return firstCompletion.Task;
            }
            secondStarted.TrySetResult();
            return Task.FromResult(new ScopedSourceSearchResult([], []));
        });
        var orchestrator = new ScopedDiscoveryOrchestrator(new Resolver(adapter), new GrantGate());
        var execution = orchestrator.ExecuteAsync(Request(), Plan(),
            new DiscoveryExecutionOptions(1, TimeSpan.FromMilliseconds(100)));
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Wait longer than the adapter timeout. The second adapter must still be blocked.
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.Equal(1, Volatile.Read(ref calls));
            Assert.False(secondStarted.Task.IsCompleted);
        }
        finally
        {
            firstCompletion.TrySetResult(new ScopedSourceSearchResult([], []));
        }

        var result = await execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, result.Sources.Count);
        Assert.Equal(SourceExecutionStatus.TimedOut, result.Sources[0].Status);
        Assert.Equal(SourceExecutionStatus.Succeeded, result.Sources[1].Status);
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    private static ScopedDiscoveryPlanningRequest Request() =>
        new(new SearchRequest([new CountryCode("PT")], [], [], [], null, [], []),
            new DiscoveryGeographyIntent(PublicationMarketIntent.KnownCountries(
                [new CountryCode("PT"), new CountryCode("GB")]), []));
    private static ScopedDiscoveryPlan Plan() =>
        new([
            new ScopedPlannedTarget(new ScopedSourceExecutionTarget(new SourceId("provider"),
                new SourceExecutionScope.PublicationCountry(new CountryCode("GB"))),
                [ScopedSourcePlanReason.Eligible], []),
            new ScopedPlannedTarget(new ScopedSourceExecutionTarget(new SourceId("provider"),
                new SourceExecutionScope.PublicationCountry(new CountryCode("PT"))),
                [ScopedSourcePlanReason.Eligible], [])
        ]);
    private sealed class Resolver(IScopedOpportunitySourceAdapter adapter) : IScopedOpportunitySourceAdapterResolver
    {
        public IScopedOpportunitySourceAdapter? Find(SourceId sourceId) => adapter;
    }
    private sealed class ControlledAdapter(
        Func<ScopedSourceExecutionRequest, CancellationToken, Task<ScopedSourceSearchResult>> search)
        : IScopedOpportunitySourceAdapter
    {
        public SourceId SourceId => new("provider");
        public Task<ScopedSourceSearchResult> SearchAsync(
            ScopedSourceExecutionRequest request, CancellationToken cancellationToken) =>
            search(request, cancellationToken);
    }
    private sealed class GrantGate : IScopedSourceExecutionGate
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
