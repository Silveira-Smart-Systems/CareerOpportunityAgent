using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using COA.Domain.Matching;
using COA.Domain.Opportunity;

namespace COA.Web.Api;

public sealed record OpportunityImportRequest(string Url, string Title, string Description);

public sealed record OpportunityImportResult(Opportunity Opportunity, MatchAssessment Assessment);

public sealed record ScoredOpportunity(Opportunity Opportunity, MatchAssessment Assessment);

/// <summary>
/// Server-side client for the COA API, authenticated with the current user's OIDC access token.
/// </summary>
public sealed class CoaApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    public async Task<IReadOnlyList<ScoredOpportunity>> GetOpportunitiesAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/opportunities", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ScoredOpportunity>>(cancellationToken) ?? [];
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

        return await response.Content.ReadFromJsonAsync<OpportunityImportResult>(cancellationToken)
            ?? throw new CoaApiRequestException(response.StatusCode, "The COA API returned an empty response.");
    }

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
