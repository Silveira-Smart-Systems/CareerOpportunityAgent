using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using COA.Domain.Matching;
using COA.Domain.Opportunity;

namespace COA.Web.Api;

public sealed record OpportunityImportRequest(string Url, string Title, string Description);

public sealed record OpportunityImportResult(Opportunity Opportunity, MatchAssessment Assessment);

public sealed record ScoredOpportunity(Opportunity Opportunity, MatchAssessment Assessment);
public sealed record CareerProfileDto(Guid Id, int Version, string Headline, IReadOnlyList<ProfileEvidenceDto> Evidence, CareerPreferencesDto Preferences);
public sealed record ProfileEvidenceDto(string Claim, string SourceReference, string Origin);
public sealed record CareerPreferencesDto(string PreferredArrangement, bool RemoteOnly, decimal? MinimumHourlyRate, string? Currency, int? MaximumOfficeDaysPerWeek);
public sealed record CareerProfileRequest(string Headline, IReadOnlyList<ProfileEvidenceDto> Evidence, CareerPreferencesDto Preferences, int? ExpectedVersion = null);
public sealed record SearchProfileDto(Guid Id, int Version, string Name, bool IsActive, SearchPreferencesDto Preferences);
public sealed record SearchPreferencesDto(IReadOnlyList<string> Categories, IReadOnlyList<string> TargetRoles, IReadOnlyList<string> WorkModes, IReadOnlyList<string> SourceMarkets, string? RemoteFromCountry, IReadOnlyList<string> EmploymentTypes, IReadOnlyList<string> Languages, IReadOnlyList<string> Skills, IReadOnlyList<string> ExcludedKeywords, IReadOnlyList<string> MandatoryCriteria, IReadOnlyList<string> PreferredCriteria);
public sealed record SearchProfileRequest(string Name, SearchPreferencesDto Preferences, int? ExpectedVersion = null);

/// <summary>
/// Server-side client for the COA API, authenticated with the current user's OIDC access token.
/// </summary>
public sealed class CoaApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };
    public async Task<IReadOnlyList<ScoredOpportunity>> GetOpportunitiesAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/opportunities", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ScoredOpportunity>>(ApiJsonOptions, cancellationToken) ?? [];
    }

    public async Task<OpportunityImportResult> ImportOpportunityAsync(
        OpportunityImportRequest opportunity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opportunity);

        using var request = await CreateRequestAsync(HttpMethod.Post, "api/opportunities", cancellationToken);
        request.Content = JsonContent.Create(opportunity);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<OpportunityImportResult>(ApiJsonOptions, cancellationToken)
            ?? throw new CoaApiRequestException(response.StatusCode, "The COA API returned an empty response.");
    }
    public async Task<ScoredOpportunity?> GetOpportunityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/opportunities/{id}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ScoredOpportunity>(ApiJsonOptions, cancellationToken);
    }

    public async Task<CareerProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/profile", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CareerProfileDto>(ApiJsonOptions, cancellationToken);
    }

    public async Task<CareerProfileDto> SaveProfileAsync(CareerProfileRequest profile, bool exists, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(exists ? HttpMethod.Put : HttpMethod.Post, "api/profile", cancellationToken);
        request.Content = exists
            ? JsonContent.Create(new { expectedVersion = profile.ExpectedVersion, profile.Headline, profile.Evidence, profile.Preferences })
            : JsonContent.Create(new { profile.Headline, profile.Evidence, profile.Preferences });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CareerProfileDto>(ApiJsonOptions, cancellationToken) ?? throw new CoaApiRequestException(response.StatusCode, "The COA API returned an empty profile.");
    }

    public async Task<IReadOnlyList<SearchProfileDto>> GetSearchProfilesAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/search-profiles", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<SearchProfileDto>>(ApiJsonOptions, cancellationToken) ?? [];
    }

    public async Task<SearchProfileDto> CreateSearchProfileAsync(SearchProfileRequest profile, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/search-profiles", cancellationToken);
        request.Content = JsonContent.Create(new { profile.Name, profile.Preferences });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SearchProfileDto>(ApiJsonOptions, cancellationToken) ?? throw new CoaApiRequestException(response.StatusCode, "The COA API returned an empty search profile.");
    }
    public async Task UpdateSearchProfileAsync(Guid id, SearchProfileRequest profile, CancellationToken cancellationToken = default)
    { using var request=await CreateRequestAsync(HttpMethod.Put,$"api/search-profiles/{id}",cancellationToken); request.Content=JsonContent.Create(new { expectedVersion=profile.ExpectedVersion, profile.Name, profile.Preferences }); using var response=await httpClient.SendAsync(request,cancellationToken); await EnsureSuccessAsync(response,cancellationToken); }
    public async Task SetSearchProfileActiveAsync(SearchProfileDto profile, bool active, CancellationToken cancellationToken = default)
    { using var request=await CreateRequestAsync(HttpMethod.Patch,$"api/search-profiles/{profile.Id}/activation",cancellationToken); request.Content=JsonContent.Create(new { expectedVersion=profile.Version, isActive=active }); using var response=await httpClient.SendAsync(request,cancellationToken); await EnsureSuccessAsync(response,cancellationToken); }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new CoaApiAuthenticationException("No authenticated HTTP session is available.");
        var authentication = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var accessToken = authentication.Properties?.GetTokenValue("access_token");

        if (!authentication.Succeeded || string.IsNullOrWhiteSpace(accessToken))
        {
            throw new CoaApiAuthenticationException(
                "An access token is unavailable or expired. Sign in again; token refresh is not implemented.");
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new CoaApiAuthenticationException(
                "The COA API rejected the access token. Sign in again; token refresh is not implemented.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new CoaApiAuthorizationException();
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new CoaApiConflictException();
        }

        throw new CoaApiRequestException(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }
}

public sealed class CoaApiAuthenticationException(string message) : InvalidOperationException(message);

public sealed class CoaApiAuthorizationException()
    : InvalidOperationException("The current user is not authorized to access the COA API.");

public sealed class CoaApiConflictException()
    : InvalidOperationException("The COA API reported a conflict.");

public sealed class CoaApiRequestException(HttpStatusCode statusCode, string message) : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
