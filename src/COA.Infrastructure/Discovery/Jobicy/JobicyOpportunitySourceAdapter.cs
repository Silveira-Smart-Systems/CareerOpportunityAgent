using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using COA.Application.Discovery;

namespace COA.Infrastructure.Discovery.Jobicy;

public sealed class JobicyOpportunitySourceAdapter : IOpportunitySourceAdapter
{
    public static readonly SourceId JobicySourceId = new("jobicy");
    private readonly HttpClient _httpClient;
    private readonly JobicyAdapterOptions _options;
    private readonly IJobicyRequestRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public JobicyOpportunitySourceAdapter(HttpClient httpClient, JobicyAdapterOptions options,
        IJobicyRequestRateLimiter rateLimiter, TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _options.Validate();
    }

    public SourceId SourceId => JobicySourceId;

    public async Task<SourceSearchResult> SearchAsync(SourceExecutionTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.SourceId != SourceId) throw new ArgumentException("The target belongs to another source.", nameof(target));
        if (!_options.Enabled) throw new InvalidOperationException("The experimental Jobicy adapter is disabled.");
        if (target.Criteria.Any(criterion => criterion.IsMandatory &&
            criterion.Disposition is CriterionDisposition.AppliedByProvider or CriterionDisposition.AppliedPostFetch))
            throw new InvalidOperationException("Jobicy has no approved mandatory filter semantics.");
        if (!_options.TryGetGeo(target.Market, out var geo))
            throw new InvalidOperationException("No explicit Jobicy geo mapping exists for the requested market.");

        using var timeout = new CancellationTokenSource(_options.RequestTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var candidates = new List<SourceOpportunityCandidate>();
        var externalIds = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;

        for (var page = 0; page < _options.MaximumPages; page++)
        {
            var response = await GetPageAsync(geo, cursor, linked.Token);
            foreach (var job in response.Jobs ?? [])
            {
                if (!TryCreateCandidate(job, target.Market, out var candidate) || !externalIds.Add(candidate.Provenance.ExternalId))
                    continue;
                candidates.Add(candidate);
            }

            cursor = response.NextCursor;
            if (string.IsNullOrWhiteSpace(cursor)) break;
        }

        // No Jobicy filter semantics are approved in this experimental adapter.
        return new SourceSearchResult(candidates.AsReadOnly(), []);
    }

    private async Task<JobicyJobsResponse> GetPageAsync(string geo, string? cursor, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, CreateJobsUri(geo, cursor));
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
            {
                var delay = GetRetryDelay(response.Headers.RetryAfter);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, _timeProvider, cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var parsed = await JsonSerializer.DeserializeAsync<JobicyJobsResponse>(body, SerializerOptions, cancellationToken);
            return parsed ?? throw new JsonException("Jobicy returned an empty JSON document.");
        }
    }

    private Uri CreateJobsUri(string geo, string? cursor)
    {
        var query = $"count={_options.PageSize}&geo={Uri.EscapeDataString(geo)}";
        if (!string.IsNullOrWhiteSpace(cursor)) query += $"&cursor={Uri.EscapeDataString(cursor)}";
        return new Uri(_options.BaseAddress, $"api/jobs?{query}");
    }

    private TimeSpan GetRetryDelay(System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter)
    {
        var delay = retryAfter?.Delta ?? (retryAfter?.Date - _timeProvider.GetUtcNow()) ?? TimeSpan.FromSeconds(1);
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        return delay > _options.MaximumRetryDelay ? _options.MaximumRetryDelay : delay;
    }

    private bool TryCreateCandidate(JobicyJob job, CountryCode market, out SourceOpportunityCandidate candidate)
    {
        candidate = default!;
        var externalId = job.Id?.Trim();
        var sourceUrlText = job.Url ?? job.JobUrl;
        if (string.IsNullOrWhiteSpace(externalId) || !Uri.TryCreate(sourceUrlText, UriKind.Absolute, out var sourceUrl) ||
            sourceUrl.Scheme != Uri.UriSchemeHttps || !sourceUrl.Host.Equals("jobicy.com", StringComparison.OrdinalIgnoreCase) &&
            !sourceUrl.Host.EndsWith(".jobicy.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var fields = new Dictionary<string, SourceObservedValue>(StringComparer.Ordinal)
        {
            ["title"] = Observed(job.JobTitle),
            ["company"] = Observed(job.CompanyName),
            ["jobGeo"] = Observed(job.JobGeo),
            ["publishedAt"] = Observed(job.PubDate),
            ["employmentType"] = Observed(job.JobType),
            ["descriptionHtml"] = Observed(job.JobDescription),
            ["salaryMinimum"] = Observed(job.AnnualSalaryMin),
            ["salaryMaximum"] = Observed(job.AnnualSalaryMax),
            ["salaryCurrency"] = Observed(job.SalaryCurrency)
        };
        candidate = new SourceOpportunityCandidate(
            new SourceProvenance(SourceId, externalId, sourceUrl, market, _timeProvider.GetUtcNow()),
            new ReadOnlyDictionary<string, SourceObservedValue>(fields));
        return true;
    }

    private static SourceObservedValue Observed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? new(null, SourceFieldStatus.Unknown) : new(value, SourceFieldStatus.Present);
    private static SourceObservedValue Observed(JsonElement? value) => value is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }
        ? new(null, SourceFieldStatus.Unknown) : new(value.Value.ToString(), SourceFieldStatus.Present);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    private sealed class JobicyJobsResponse
    {
        [JsonPropertyName("jobs")] public List<JobicyJob>? Jobs { get; init; }
        [JsonPropertyName("nextCursor")] public string? NextCursor { get; init; }
    }

    private sealed class JobicyJob
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("url")] public string? Url { get; init; }
        [JsonPropertyName("jobUrl")] public string? JobUrl { get; init; }
        [JsonPropertyName("jobTitle")] public string? JobTitle { get; init; }
        [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
        [JsonPropertyName("jobGeo")] public string? JobGeo { get; init; }
        [JsonPropertyName("pubDate")] public string? PubDate { get; init; }
        [JsonPropertyName("jobType")] public string? JobType { get; init; }
        [JsonPropertyName("jobDescription")] public string? JobDescription { get; init; }
        [JsonPropertyName("annualSalaryMin")] public JsonElement? AnnualSalaryMin { get; init; }
        [JsonPropertyName("annualSalaryMax")] public JsonElement? AnnualSalaryMax { get; init; }
        [JsonPropertyName("salaryCurrency")] public string? SalaryCurrency { get; init; }
    }
}
