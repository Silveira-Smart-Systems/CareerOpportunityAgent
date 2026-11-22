using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class InMemoryScopedExecutionReservationAuthorityTests
{
    [Fact]
    public async Task Concurrent_requests_cannot_reserve_the_same_slot()
    {
        var authority = Authority(capacity: 1);
        var requests = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => authority.ValidateAndReserveAtomicallyAsync(
                Request(), CancellationToken.None))).ToArray();
        var decisions = await Task.WhenAll(requests);
        Assert.Single(decisions.OfType<ScopedSourceExecutionAuthorization.Granted>());
        Assert.Equal(63, decisions.OfType<ScopedSourceExecutionAuthorization.Blocked>()
            .Count(x => x.Reason == ScopedSourceExecutionBlockReason.ReservationUnavailable));
    }

    [Fact]
    public async Task Releasing_unstarted_permit_refunds_capacity_only_once()
    {
        var authority = Authority(capacity: 1);
        var granted = Assert.IsType<ScopedSourceExecutionAuthorization.Granted>(
            await authority.ValidateAndReserveAtomicallyAsync(Request(), CancellationToken.None));
        await granted.Permit.ReleaseAsync(CancellationToken.None);
        await granted.Permit.ReleaseAsync(CancellationToken.None);
        Assert.IsType<ScopedSourceExecutionAuthorization.Granted>(
            await authority.ValidateAndReserveAtomicallyAsync(Request(), CancellationToken.None));
        Assert.IsType<ScopedSourceExecutionAuthorization.Blocked>(
            await authority.ValidateAndReserveAtomicallyAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Started_permit_cannot_be_refunded()
    {
        var authority = Authority(capacity: 1);
        var granted = Assert.IsType<ScopedSourceExecutionAuthorization.Granted>(
            await authority.ValidateAndReserveAtomicallyAsync(Request(), CancellationToken.None));
        await granted.Permit.MarkAttemptStartedAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            granted.Permit.ReleaseAsync(CancellationToken.None));
        await granted.Permit.ConsumeAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            granted.Permit.ConsumeAsync(CancellationToken.None));
        Assert.IsType<ScopedSourceExecutionAuthorization.Blocked>(
            await authority.ValidateAndReserveAtomicallyAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Unverified_scope_fails_closed()
    {
        var authority = Authority(capacity: 1, verified: false);
        var blocked = Assert.IsType<ScopedSourceExecutionAuthorization.Blocked>(
            await authority.ValidateAndReserveAtomicallyAsync(Request(), CancellationToken.None));
        Assert.Equal(ScopedSourceExecutionBlockReason.EvidenceNotVerified, blocked.Reason);
    }

    [Fact]
    public async Task Cancelled_request_does_not_reserve_capacity()
    {
        var authority = Authority(capacity: 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authority.ValidateAndReserveAtomicallyAsync(Request(), cancellation.Token));
        Assert.IsType<ScopedSourceExecutionAuthorization.Granted>(
            await authority.ValidateAndReserveAtomicallyAsync(Request(), CancellationToken.None));
    }

    private static InMemoryScopedExecutionReservationAuthority Authority(int capacity, bool verified = true)
    {
        var authority = new InMemoryScopedExecutionReservationAuthority();
        var id = new SourceId("example");
        var capabilities = new SourceCapabilities(id, [new CountryCode("PT")],
            [new KeyValuePair<SearchCriterion, FilterSupport>(
                SearchCriterion.TargetRole, FilterSupport.SupportedByProvider)]);
        var state = new SourcePlanningState(capabilities,
            new SourceExecutionPolicy(true, new SourceLicenseRights(true, false, false),
                new RateLimitPolicy(null), new SourceBudgetPolicy(null)),
            new SourceRuntimeStatus(SourceHealth.Healthy, CapacityAvailability.Available,
                CapacityAvailability.Available, [], DateTimeOffset.UtcNow));
        authority.RegisterVerified(new VerifiedScopedSourceRegistration(state,
            verified ? [new SourceExecutionScope.UnconstrainedPublication()] : [], capacity));
        return authority;
    }

    private static ScopedSourceExecutionRequest Request() =>
        new(new ScopedSourceExecutionTarget(new SourceId("example"),
                new SourceExecutionScope.UnconstrainedPublication()),
            new SearchRequest([new CountryCode("PT")], [], [], [], null,
                [SearchCriterion.TargetRole], []),
            [new PlannedCriterion(SearchCriterion.TargetRole, true,
                CriterionDisposition.AppliedByProvider)]);
}
