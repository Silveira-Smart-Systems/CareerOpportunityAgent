using System.Net;
using System.Net.Http.Headers;
using System.Text;
using COA.Application.Discovery;
using COA.Infrastructure.Discovery.Jobicy;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class JobicyOpportunitySourceAdapterTests
{
    private static readonly CountryCode Gb = new("GB");

    [Fact]
    public async Task Disabled_by_default_makes_no_http_request()
    {
        var handler = new SequenceHandler();
        var adapter = Create(handler, new JobicyAdapterOptions { MarketGeo = Geo() });

        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.SearchAsync(Target(), CancellationToken.None));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Maps_observed_fields_and_preserves_provenance_without_remote_eligibility()
    {
        var handler = new SequenceHandler(Json("""
            {"jobs":[{"id":"job-1","url":"https://jobicy.com/jobs/job-1","jobTitle":"Engineer","companyName":"Acme","jobGeo":"France, UK","pubDate":"2026-11-21T10:00:00Z","jobType":"Full-Time","jobDescription":"<p>Untrusted</p>","annualSalaryMin":1200,"salaryCurrency":"EUR"}]}
            """));
        var adapter = Create(handler, EnabledOptions());

        var result = await adapter.SearchAsync(Target(), CancellationToken.None);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(JobicyOpportunitySourceAdapter.JobicySourceId, candidate.Provenance.SourceId);
        Assert.Equal("job-1", candidate.Provenance.ExternalId);
        Assert.Equal(Gb, candidate.Provenance.RequestedMarket);
        Assert.Equal("France, UK", candidate.Fields["jobGeo"].Value);
        Assert.Equal(SourceFieldStatus.Present, candidate.Fields["descriptionHtml"].Status);
        Assert.Equal("1200", candidate.Fields["salaryMinimum"].Value);
        Assert.Equal(SourceFieldStatus.Unknown, candidate.Fields["salaryMaximum"].Status);
        Assert.Empty(result.AppliedCriteria);
        Assert.DoesNotContain(candidate.Fields.Keys, key => key.Contains("Remote", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Follows_a_limited_cursor_and_deduplicates_external_ids()
    {
        var handler = new SequenceHandler(
            Json("""{"nextCursor":"next","jobs":[{"id":"same","url":"https://jobicy.com/jobs/same"}]}"""),
            Json("""{"jobs":[{"id":"same","url":"https://jobicy.com/jobs/same"},{"id":"other","url":"https://jobicy.com/jobs/other"}]}"""));
        var adapter = Create(handler, EnabledOptions(maximumPages: 2));

        var result = await adapter.SearchAsync(Target(), CancellationToken.None);

        Assert.Equal(["same", "other"], result.Candidates.Select(candidate => candidate.Provenance.ExternalId));
        Assert.Equal(2, handler.Calls);
        Assert.Contains("cursor=next", handler.RequestUris[1].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retries_429_once_honouring_retry_after_without_an_unbounded_loop()
    {
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        var handler = new SequenceHandler(throttled, Json("""{"jobs":[]}"""));
        var adapter = Create(handler, EnabledOptions());

        var result = await adapter.SearchAsync(Target(), CancellationToken.None);

        Assert.Empty(result.Candidates);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Malformed_json_and_cancellation_are_not_hidden()
    {
        var malformed = Create(new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{", Encoding.UTF8, "application/json") }), EnabledOptions());
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => malformed.SearchAsync(Target(), CancellationToken.None));

        using var cancellation = new CancellationTokenSource();
        var slow = Create(new CancellingHandler(), EnabledOptions(requestTimeout: TimeSpan.FromMilliseconds(1)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slow.SearchAsync(Target(), cancellation.Token));
    }

    [Fact]
    public async Task Wrong_source_unmapped_market_and_mandatory_filter_fail_before_http()
    {
        var handler = new SequenceHandler();
        var adapter = Create(handler, EnabledOptions());
        var mandatory = new[] { new PlannedCriterion(SearchCriterion.Skill, true, CriterionDisposition.AppliedByProvider) };

        await Assert.ThrowsAsync<ArgumentException>(() => adapter.SearchAsync(Target(sourceId: new SourceId("other")), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.SearchAsync(Target(criteria: mandatory), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.SearchAsync(Target(market: new CountryCode("PT")), CancellationToken.None));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Unsupported_mandatory_criterion_is_blocked_by_planner_before_gate_or_http()
    {
        var request = new SearchRequest([Gb], ["Technology"], ["Architect"], ["Remote"], null,
            [SearchCriterion.TargetRole], []);
        var state = new SourcePlanningState(
            new SourceCapabilities(JobicyOpportunitySourceAdapter.JobicySourceId, [Gb],
                [new KeyValuePair<SearchCriterion, FilterSupport>(SearchCriterion.TargetRole, FilterSupport.UnknownSemantics)]),
            new SourceExecutionPolicy(true, new SourceLicenseRights(true, false, false), new RateLimitPolicy(null), new SourceBudgetPolicy(null)),
            new SourceRuntimeStatus(SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, [], DateTimeOffset.UnixEpoch));
        var plan = new DiscoveryPlanner().CreatePlan(request, new DiscoveryPlanningSnapshot([state]));
        var handler = new SequenceHandler();
        var gate = new RecordingGate();
        var adapter = Create(handler, EnabledOptions());
        var orchestrator = new DiscoveryOrchestrator(new SingleAdapterResolver(adapter), gate);

        var outcome = await orchestrator.ExecuteAsync(request, plan, new DiscoveryExecutionOptions(1, TimeSpan.FromSeconds(1)));

        Assert.Empty(outcome.Sources); Assert.Equal(0, gate.Calls); Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Gate_denial_prevents_an_otherwise_eligible_adapter_http_call()
    {
        var request = new SearchRequest([Gb], ["Technology"], ["Architect"], ["Remote"], null, [], []);
        var state = new SourcePlanningState(
            new SourceCapabilities(JobicyOpportunitySourceAdapter.JobicySourceId, [Gb], []),
            new SourceExecutionPolicy(true, new SourceLicenseRights(true, false, false), new RateLimitPolicy(null), new SourceBudgetPolicy(null)),
            new SourceRuntimeStatus(SourceHealth.Healthy, CapacityAvailability.Available, CapacityAvailability.Available, [], DateTimeOffset.UnixEpoch));
        var plan = new DiscoveryPlanner().CreatePlan(request, new DiscoveryPlanningSnapshot([state]));
        var handler = new SequenceHandler();
        var gate = new RecordingGate(new SourceExecutionAuthorization.Blocked(SourceExecutionBlockReason.LicenseDenied));
        var outcome = await new DiscoveryOrchestrator(new SingleAdapterResolver(Create(handler, EnabledOptions())), gate)
            .ExecuteAsync(request, plan, new DiscoveryExecutionOptions(1, TimeSpan.FromSeconds(1)));

        Assert.Equal(SourceExecutionStatus.Blocked, Assert.Single(outcome.Sources).Status);
        Assert.Equal(1, gate.Calls); Assert.Equal(0, handler.Calls);
    }

    private static JobicyOpportunitySourceAdapter Create(HttpMessageHandler handler, JobicyAdapterOptions options) =>
        new(new HttpClient(handler), options, new NoDelayRateLimiter());
    private static JobicyAdapterOptions EnabledOptions(int maximumPages = 1, TimeSpan? requestTimeout = null) => new()
    { Enabled = true, MarketGeo = Geo(), MaximumPages = maximumPages, RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(1), MaximumRetryDelay = TimeSpan.Zero };
    private static Dictionary<string, string> Geo() => new(StringComparer.OrdinalIgnoreCase) { ["GB"] = "europe" };
    private static SourceExecutionTarget Target(SourceId? sourceId = null, CountryCode? market = null, IReadOnlyList<PlannedCriterion>? criteria = null) =>
        new(sourceId ?? JobicyOpportunitySourceAdapter.JobicySourceId, market ?? Gb,
            new SearchRequest([market ?? Gb], ["Technology"], ["Architect"], ["Remote"], null, [], []), criteria ?? []);
    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class NoDelayRateLimiter : IJobicyRequestRateLimiter { public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class SingleAdapterResolver(IOpportunitySourceAdapter adapter) : IOpportunitySourceAdapterResolver { public IOpportunitySourceAdapter? Find(SourceId sourceId) => adapter.SourceId == sourceId ? adapter : null; }
    private sealed class RecordingGate(SourceExecutionAuthorization? authorization = null) : ISourceExecutionGate
    {
        public int Calls { get; private set; }
        public Task<SourceExecutionAuthorization> AuthorizeAndReserveAsync(SourceExecutionTarget target, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(authorization ?? new SourceExecutionAuthorization.Blocked(SourceExecutionBlockReason.NotAuthorized)); }
    }
    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public int Calls { get; private set; }
        public List<Uri> RequestUris { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; RequestUris.Add(request.RequestUri!);
            return Task.FromResult(_responses.Count == 0 ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"jobs\":[]}") } : _responses.Dequeue());
        }
    }
    private sealed class CancellingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return new HttpResponseMessage(HttpStatusCode.OK); }
    }
}
