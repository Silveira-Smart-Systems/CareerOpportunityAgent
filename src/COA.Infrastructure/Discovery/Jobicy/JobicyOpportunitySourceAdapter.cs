using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using COA.Application.Discovery;

namespace COA.Infrastructure.Discovery.Jobicy;

public sealed class JobicyOpportunitySourceAdapter(JobicyJobsTransport transport) : IOpportunitySourceAdapter
{
    public static readonly SourceId JobicySourceId = new("jobicy");
    public SourceId SourceId => JobicySourceId;

    public Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.SourceId != SourceId) throw new ArgumentException("The target belongs to another source.", nameof(target));
        if (!transport.Options.Enabled) throw new InvalidOperationException("The experimental Jobicy adapter is disabled.");
        if (target.Criteria.Any(criterion => criterion.IsMandatory))
            throw new InvalidOperationException("Jobicy has no approved mandatory filter semantics.");

        // Jobicy geo is remote-work eligibility geography; SourceMarkets is source publication
        // market. No evidenced conversion exists, so do not label a broader geo response as target.Market.
        throw new InvalidOperationException("Jobicy geo cannot be safely mapped to SourceMarkets.");
    }

    // Reserved for a later, approved source-market contract with evidence for the query scope.
    internal static bool TryCreateCandidate(JobicyJob job, CountryCode market, DateTimeOffset retrievedAtUtc,
        out SourceOpportunityCandidate candidate)
    {
        candidate = default!;
        if (job.Id is null || !Uri.TryCreate(job.Url ?? job.JobUrl, UriKind.Absolute, out var sourceUrl) ||
            sourceUrl.Scheme != Uri.UriSchemeHttps || (!sourceUrl.Host.Equals("jobicy.com", StringComparison.OrdinalIgnoreCase) &&
            !sourceUrl.Host.EndsWith(".jobicy.com", StringComparison.OrdinalIgnoreCase))) return false;

        var fields = new Dictionary<string, SourceObservedValue>(StringComparer.Ordinal)
        {
            ["title"] = Observed(job.JobTitle), ["company"] = Observed(job.CompanyName),
            ["jobGeo"] = Observed(job.JobGeo), ["publishedAt"] = Observed(job.PubDate),
            ["employmentType"] = Observed(job.JobType is { Count: > 0 } ? string.Join(", ", job.JobType) : null),
            ["descriptionHtml"] = Observed(job.JobDescription), ["salaryMinimum"] = Observed(job.SalaryMin),
            ["salaryMaximum"] = Observed(job.SalaryMax), ["salaryPeriod"] = Observed(job.SalaryPeriod),
            ["salaryCurrency"] = Observed(job.SalaryCurrency)
        };
        candidate = new SourceOpportunityCandidate(
            new SourceProvenance(JobicySourceId, job.Id.Value.ToString(CultureInfo.InvariantCulture), sourceUrl, market, retrievedAtUtc),
            new ReadOnlyDictionary<string, SourceObservedValue>(fields));
        return true;
    }

    private static SourceObservedValue Observed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? new(null, SourceFieldStatus.Unknown) : new(value, SourceFieldStatus.Present);
    private static SourceObservedValue Observed(decimal? value) => value is null
        ? new(null, SourceFieldStatus.Unknown) : new(value.Value.ToString(CultureInfo.InvariantCulture), SourceFieldStatus.Present);
}

public sealed class JobicyJobsTransport(IHttpClientFactory httpClientFactory, JobicyAdapterOptions options,
    IJobicyRequestRateLimiter rateLimiter, TimeProvider? timeProvider = null)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly IJobicyRequestRateLimiter _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    public JobicyAdapterOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    internal async Task<JobicyJobsResponse> GetPageAsync(string geo, string? cursor, CancellationToken cancellationToken)
    {
        Options.Validate();
        for (var attempt = 0; ; attempt++)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            using var httpClient = _httpClientFactory.CreateClient(JobicyAdapterOptions.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, CreateJobsUri(geo, cursor));
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
            {
                var delay = GetRetryDelay(response.Headers.RetryAfter);
                if (delay is null) response.EnsureSuccessStatusCode();
                if (delay > TimeSpan.Zero) await Task.Delay(delay.Value, _timeProvider, cancellationToken);
                continue;
            }
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var parsed = await JsonSerializer.DeserializeAsync<JobicyJobsResponse>(body, SerializerOptions, cancellationToken);
            if (parsed is null) throw new JsonException("Jobicy returned an empty JSON document.");
            if (parsed.Success is false) throw new HttpRequestException("Jobicy returned success=false.");
            return parsed;
        }
    }

    private Uri CreateJobsUri(string geo, string? cursor)
    {
        var query = $"count={Options.PageSize}&geo={Uri.EscapeDataString(geo)}";
        if (!string.IsNullOrWhiteSpace(cursor)) query += $"&cursor={Uri.EscapeDataString(cursor)}";
        return new Uri(Options.BaseAddress, $"api/jobs?{query}");
    }

    private TimeSpan? GetRetryDelay(System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter)
    {
        var delay = retryAfter?.Delta ?? (retryAfter?.Date - _timeProvider.GetUtcNow()) ?? TimeSpan.FromSeconds(1);
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        return delay > Options.MaximumRetryDelay ? null : delay;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip };
}

internal sealed class JobicyJobsResponse
{
    [JsonPropertyName("success")] public bool? Success { get; init; }
    [JsonPropertyName("jobs")] public List<JobicyJob>? Jobs { get; init; }
    [JsonPropertyName("nextCursor")] public string? NextCursor { get; init; }
}

internal sealed class JobicyJob
{
    [JsonPropertyName("id")] public long? Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("jobUrl")] public string? JobUrl { get; init; }
    [JsonPropertyName("jobTitle")] public string? JobTitle { get; init; }
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
    [JsonPropertyName("jobGeo")] public string? JobGeo { get; init; }
    [JsonPropertyName("pubDate")] public string? PubDate { get; init; }
    [JsonPropertyName("jobType")] public List<string>? JobType { get; init; }
    [JsonPropertyName("jobDescription")] public string? JobDescription { get; init; }
    [JsonPropertyName("salaryMin")] public decimal? SalaryMin { get; init; }
    [JsonPropertyName("salaryMax")] public decimal? SalaryMax { get; init; }
    [JsonPropertyName("salaryPeriod")] public string? SalaryPeriod { get; init; }
    [JsonPropertyName("salaryCurrency")] public string? SalaryCurrency { get; init; }
}
