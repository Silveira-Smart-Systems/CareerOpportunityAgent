using System.Net;
using System.Net.Http.Headers;
using System.Text;
using COA.Application.Discovery;
using COA.Infrastructure.Discovery;
using COA.Infrastructure.Discovery.Jobicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class JobicyOpportunitySourceAdapterTests
{
    private static readonly CountryCode Gb = new("GB");

    [Fact]
    public async Task Disabled_by_default_makes_no_http_request()
    {
        var handler = new SequenceHandler();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAdapter(handler, new()).SearchAsync(Target(), CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Official_json_contract_maps_integer_id_array_contract_and_salary_fields()
    {
        var transport = CreateTransport(new SequenceHandler(Json("""
            {"success":true,"jobs":[{"id":123456,"url":"https://jobicy.com/jobs/one","jobType":["Full-Time","Contract"],"salaryMin":1200.50,"salaryMax":2400,"salaryPeriod":"year","salaryCurrency":"EUR","jobGeo":"France, UK"}]}
            """)), Enabled());

        var response = await transport.GetPageAsync("europe", null, CancellationToken.None);
        Assert.True(JobicyOpportunitySourceAdapter.TryCreateCandidate(Assert.Single(response.Jobs!), Gb, DateTimeOffset.UnixEpoch, out var candidate));

        Assert.Equal("123456", candidate.Provenance.ExternalId);
        Assert.Equal("Full-Time, Contract", candidate.Fields["employmentType"].Value);
        Assert.Equal("1200.50", candidate.Fields["salaryMinimum"].Value);
        Assert.Equal("2400", candidate.Fields["salaryMaximum"].Value);
        Assert.Equal("year", candidate.Fields["salaryPeriod"].Value);
        Assert.Equal("France, UK", candidate.Fields["jobGeo"].Value);
    }

    [Fact]
    public void Absent_official_optional_fields_remain_unknown()
    {
        var job = new JobicyJob { Id = 7, Url = "https://jobicy.com/jobs/7" };
        Assert.True(JobicyOpportunitySourceAdapter.TryCreateCandidate(job, Gb, DateTimeOffset.UnixEpoch, out var candidate));
        Assert.Equal(SourceFieldStatus.Unknown, candidate.Fields["employmentType"].Status);
        Assert.Equal(SourceFieldStatus.Unknown, candidate.Fields["salaryMinimum"].Status);
        Assert.Equal(SourceFieldStatus.Unknown, candidate.Fields["salaryMaximum"].Status);
        Assert.Equal(SourceFieldStatus.Unknown, candidate.Fields["salaryPeriod"].Status);
    }

    [Fact]
    public async Task Geo_cannot_be_used_as_a_source_market_and_fails_closed_without_http()
    {
        var handler = new SequenceHandler(Json("""{"success":true,"jobs":[{"id":1,"url":"https://jobicy.com/jobs/1","jobGeo":"France, Poland"}]}"""));
        var adapter = CreateAdapter(handler, Enabled());

        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.SearchAsync(Target(), CancellationToken.None));

        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(CriterionDisposition.AppliedByProvider)]
    [InlineData(CriterionDisposition.AppliedPostFetch)]
    [InlineData(CriterionDisposition.Unsupported)]
    [InlineData(CriterionDisposition.UnknownSemantics)]
    public async Task Every_direct_mandatory_disposition_is_rejected_before_http(CriterionDisposition disposition)
    {
        var handler = new SequenceHandler();
        var criteria = new[] { new PlannedCriterion(SearchCriterion.Skill, true, disposition) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAdapter(handler, Enabled()).SearchAsync(Target(criteria: criteria), CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Retry_after_within_ceiling_retries_once_and_second_429_is_propagated()
    {
        var first = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        first.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        var second = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var handler = new SequenceHandler(first, second);

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateTransport(handler, Enabled()).GetPageAsync("europe", null, CancellationToken.None));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Retry_after_above_ceiling_is_not_shortened_or_retried()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
        var handler = new SequenceHandler(response);

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateTransport(handler, Enabled(maximumRetryDelay: TimeSpan.FromSeconds(1))).GetPageAsync("europe", null, CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Malformed_and_unsuccessful_json_are_not_accepted()
    {
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => CreateTransport(new SequenceHandler(Json("{")), Enabled()).GetPageAsync("europe", null, CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateTransport(new SequenceHandler(Json("""{"success":false,"jobs":[]}""")), Enabled()).GetPageAsync("europe", null, CancellationToken.None));
    }

    [Fact]
    public void Adapter_does_not_capture_a_typed_http_client()
    {
        Assert.DoesNotContain(typeof(JobicyOpportunitySourceAdapter).GetConstructors().SelectMany(ctor => ctor.GetParameters()), parameter => parameter.ParameterType == typeof(HttpClient));
    }

    [Fact]
    public void Di_registers_one_disabled_adapter_and_resolver_without_capturing_a_typed_client()
    {
        var services = new ServiceCollection();
        services.AddExperimentalJobicyDiscovery(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<IOpportunitySourceAdapterResolver>();
        var adapter = Assert.IsType<JobicyOpportunitySourceAdapter>(resolver.Find(JobicyOpportunitySourceAdapter.JobicySourceId));

        Assert.Same(adapter, resolver.Find(JobicyOpportunitySourceAdapter.JobicySourceId));
        Assert.DoesNotContain(typeof(JobicyOpportunitySourceAdapter).GetConstructors().SelectMany(ctor => ctor.GetParameters()), parameter => parameter.ParameterType == typeof(HttpClient));
    }

    private static JobicyOpportunitySourceAdapter CreateAdapter(HttpMessageHandler handler, JobicyAdapterOptions options) => new(CreateTransport(handler, options));
    private static JobicyJobsTransport CreateTransport(HttpMessageHandler handler, JobicyAdapterOptions options) => new(new HandlerFactory(handler), options, new NoDelayRateLimiter());
    private static JobicyAdapterOptions Enabled(TimeSpan? maximumRetryDelay = null) => new() { Enabled = true, MaximumRetryDelay = maximumRetryDelay ?? TimeSpan.Zero };
    private static SourceExecutionTarget Target(IReadOnlyList<PlannedCriterion>? criteria = null) => new(JobicyOpportunitySourceAdapter.JobicySourceId, Gb,
        new SearchRequest([Gb], ["Technology"], ["Architect"], ["Remote"], null, [], []), criteria ?? []);
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    private sealed class NoDelayRateLimiter : IJobicyRequestRateLimiter { public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class HandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://jobicy.com/") }; }
    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(_responses.Count == 0 ? Json("""{"success":true,"jobs":[]}""") : _responses.Dequeue()); }
    }
}
