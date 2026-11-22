using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedSourceExecutionGateTests
{
    [Fact]
    public async Task Gate_delegates_the_complete_scoped_request_to_atomic_authority()
    {
        var request = Request();
        var authority = new StubAuthority((received, _) =>
        {
            Assert.Same(request, received);
            return Task.FromResult<ScopedSourceExecutionAuthorization>(
                new ScopedSourceExecutionAuthorization.Blocked(ScopedSourceExecutionBlockReason.EvidenceNotVerified));
        });
        var result = await new ScopedSourceExecutionGate(authority)
            .AuthorizeAndReserveAsync(request, CancellationToken.None);
        Assert.IsType<ScopedSourceExecutionAuthorization.Blocked>(result);
        Assert.Equal(1, authority.Calls);
    }

    [Fact]
    public async Task Gate_rejects_missing_or_mismatched_permits()
    {
        var request = Request();
        var other = new ScopedSourceExecutionTarget(
            new SourceId("other"), new SourceExecutionScope.UnconstrainedPublication());
        var authority = new StubAuthority((_, _) => Task.FromResult<ScopedSourceExecutionAuthorization>(
            new ScopedSourceExecutionAuthorization.Granted(new StubPermit(other))));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ScopedSourceExecutionGate(authority).AuthorizeAndReserveAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Gate_rejects_missing_decision()
    {
        var authority = new StubAuthority((_, _) =>
            Task.FromResult<ScopedSourceExecutionAuthorization>(null!));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ScopedSourceExecutionGate(authority).AuthorizeAndReserveAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_prevents_authority_invocation()
    {
        var authority = new StubAuthority((_, _) => throw new Exception("Must not be invoked"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ScopedSourceExecutionGate(authority).AuthorizeAndReserveAsync(Request(), cancellation.Token));
        Assert.Equal(0, authority.Calls);
    }

    [Fact]
    public void Constructor_rejects_missing_authority() =>
        Assert.Throws<ArgumentNullException>(() => new ScopedSourceExecutionGate(null!));

    private static ScopedSourceExecutionRequest Request() =>
        new(
            new ScopedSourceExecutionTarget(new SourceId("example"),
                new SourceExecutionScope.UnconstrainedPublication()),
            new SearchRequest([new CountryCode("PT")], [], [], [], null, [], []),
            []);

    private sealed class StubAuthority(
        Func<ScopedSourceExecutionRequest, CancellationToken, Task<ScopedSourceExecutionAuthorization>> callback)
        : IScopedExecutionReservationAuthority
    {
        public int Calls { get; private set; }
        public Task<ScopedSourceExecutionAuthorization> ValidateAndReserveAtomicallyAsync(
            ScopedSourceExecutionRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return callback(request, cancellationToken);
        }
    }

    private sealed class StubPermit(ScopedSourceExecutionTarget target) : ScopedSourceExecutionPermit
    {
        public override ScopedSourceExecutionTarget Target => target;
        public override Task MarkAttemptStartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task ConsumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
