using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedReservationPolicyTests
{
    [Theory]
    [InlineData(false, true, SourceHealth.Healthy, CapacityAvailability.Available,
        CapacityAvailability.Available, false, ScopedSourceExecutionBlockReason.NotAuthorized)]
    [InlineData(true, false, SourceHealth.Healthy, CapacityAvailability.Available,
        CapacityAvailability.Available, false, ScopedSourceExecutionBlockReason.LicenseDenied)]
    [InlineData(true, true, SourceHealth.Unavailable, CapacityAvailability.Available,
        CapacityAvailability.Available, false, ScopedSourceExecutionBlockReason.SourceUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Unknown,
        CapacityAvailability.Available, false, ScopedSourceExecutionBlockReason.QuotaUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Available,
        CapacityAvailability.Exhausted, false, ScopedSourceExecutionBlockReason.BudgetUnavailable)]
    [InlineData(true, true, SourceHealth.Healthy, CapacityAvailability.Available,
        CapacityAvailability.Available, true, ScopedSourceExecutionBlockReason.TemporarilyRestricted)]
    public async Task Denied_policy_never_issues_a_permit(
        bool authorized, bool licensed, SourceHealth health, CapacityAvailability quota,
        CapacityAvailability budget, bool restricted, ScopedSourceExecutionBlockReason expected)
    {
        var authority = new InMemoryScopedExecutionReservationAuthority();
        var id = new SourceId("example");
        var scope = new SourceExecutionScope.UnconstrainedPublication();
        var state = new SourcePlanningState(
            new SourceCapabilities(id, [new CountryCode("PT")], []),
            new SourceExecutionPolicy(authorized, new SourceLicenseRights(licensed, false, false),
                new RateLimitPolicy(null), new SourceBudgetPolicy(null)),
            new SourceRuntimeStatus(health, quota, budget,
                restricted ? ["temporary"] : [], DateTimeOffset.UtcNow));
        authority.RegisterVerified(new VerifiedScopedSourceRegistration(state, [scope], 1));
        var request = new ScopedSourceExecutionRequest(
            new ScopedSourceExecutionTarget(id, scope),
            new SearchRequest([new CountryCode("PT")], [], [], [], null, [], []), []);
        var result = await authority.ValidateAndReserveAtomicallyAsync(request, CancellationToken.None);
        var blocked = Assert.IsType<ScopedSourceExecutionAuthorization.Blocked>(result);
        Assert.Equal(expected, blocked.Reason);
    }

    [Fact]
    public async Task Missing_source_fails_closed_without_registration()
    {
        var authority = new InMemoryScopedExecutionReservationAuthority();
        var result = await authority.ValidateAndReserveAtomicallyAsync(
            new ScopedSourceExecutionRequest(
                new ScopedSourceExecutionTarget(new SourceId("unknown"),
                    new SourceExecutionScope.UnconstrainedPublication()),
                new SearchRequest([new CountryCode("PT")], [], [], [], null, [], []), []),
            CancellationToken.None);
        Assert.Equal(ScopedSourceExecutionBlockReason.EvidenceNotVerified,
            Assert.IsType<ScopedSourceExecutionAuthorization.Blocked>(result).Reason);
    }
}
