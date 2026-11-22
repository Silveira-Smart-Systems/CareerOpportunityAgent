using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace COA.Infrastructure.IntegrationTests;

public sealed class ApiOpportunityEndpointsTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Unauthenticated_post_and_get_are_rejected()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var get = await client.GetAsync("/api/opportunities");
        var post = await SendJsonAsync(client, OpportunityJson("https://jobs.example.test/unauthenticated", "Unauthenticated", "Synthetic role"));

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
    }

    [Fact]
    public async Task Authenticated_owner_can_import_and_list_opportunities()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authorize(client, factory.CreateToken($"opportunity-round-trip-{Guid.NewGuid():N}"));

        var create = await SendJsonAsync(client, OpportunityJson(
            "https://jobs.example.test/round-trip",
            "Remote .NET Engineer",
            "A remote .NET role"));
        var list = await client.GetAsync("/api/opportunities");
        var opportunities = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("Remote .NET Engineer", opportunities[0].GetProperty("opportunity").GetProperty("title").GetString());
        Assert.Equal(55m, opportunities[0].GetProperty("assessment").GetProperty("score").GetDecimal());
        Assert.True(opportunities[0].GetProperty("assessment").GetProperty("eligible").GetBoolean());
    }

    [Theory]
    [InlineData("not-a-url", "Valid title", "Valid description")]
    [InlineData("https://jobs.example.test/invalid", "", "Valid description")]
    [InlineData("https://jobs.example.test/invalid", "Valid title", "")]
    public async Task Authenticated_owner_cannot_import_invalid_data(string url, string title, string description)
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authorize(client, factory.CreateToken($"opportunity-invalid-{Guid.NewGuid():N}"));

        var response = await SendJsonAsync(client, OpportunityJson(url, title, description));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Owners_cannot_list_each_others_opportunities()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var ownerA = factory.CreateClient();
        using var ownerB = factory.CreateClient();
        Authorize(ownerA, factory.CreateToken($"opportunity-owner-a-{Guid.NewGuid():N}"));
        Authorize(ownerB, factory.CreateToken($"opportunity-owner-b-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Created, (await SendJsonAsync(ownerA, OpportunityJson(
            "https://jobs.example.test/private",
            "Private opportunity",
            "Synthetic role"))).StatusCode);
        var otherOwnerList = await ownerB.GetAsync("/api/opportunities");

        Assert.Equal(HttpStatusCode.OK, otherOwnerList.StatusCode);
        Assert.Equal(0, JsonDocument.Parse(await otherOwnerList.Content.ReadAsStringAsync()).RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Other_owner_cannot_get_an_opportunity_by_id()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var ownerA = factory.CreateClient();
        using var ownerB = factory.CreateClient();
        Authorize(ownerA, factory.CreateToken($"opportunity-detail-a-{Guid.NewGuid():N}"));
        Authorize(ownerB, factory.CreateToken($"opportunity-detail-b-{Guid.NewGuid():N}"));
        var create = await SendJsonAsync(ownerA, OpportunityJson("https://jobs.example.test/detail-private", "Private", "Synthetic role"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("opportunity").GetProperty("id").GetGuid();

        var ownResponse = await ownerA.GetAsync($"/api/opportunities/{id}");
        var response = await ownerB.GetAsync($"/api/opportunities/{id}");

        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_imports_return_created_and_conflict()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var token = factory.CreateToken($"opportunity-concurrent-{Guid.NewGuid():N}");
        Authorize(first, token);
        Authorize(second, token);
        var payload = OpportunityJson("https://jobs.example.test/concurrent", "Concurrent", "Synthetic role");

        var responses = await Task.WhenAll(
            SendJsonAsync(first, payload),
            SendJsonAsync(second, payload));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string json) =>
        client.PostAsync("/api/opportunities", new StringContent(json, Encoding.UTF8, "application/json"));

    private static string OpportunityJson(string url, string title, string description) =>
        JsonSerializer.Serialize(new { url, title, description });
}
