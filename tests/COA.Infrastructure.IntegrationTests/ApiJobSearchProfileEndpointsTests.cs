using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace COA.Infrastructure.IntegrationTests;
public sealed class ApiJobSearchProfileEndpointsTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Owner_can_create_list_update_activate_and_delete_search_profile()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString); using var client = factory.CreateClient(); Authorize(client, factory.CreateToken("search-owner"));
        var create = await Send(client, HttpMethod.Post, "/api/search-profiles", Payload("Architecture", null));
        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement; var id = created.GetProperty("id").GetGuid();
        var list = await client.GetAsync("/api/search-profiles");
        var update = await Send(client, HttpMethod.Put, $"/api/search-profiles/{id}", Payload("Leadership", 1));
        var deactivate = await Send(client, HttpMethod.Patch, $"/api/search-profiles/{id}/activation", "{\"expectedVersion\":2,\"isActive\":false}");
        var delete = await client.DeleteAsync($"/api/search-profiles/{id}?expectedVersion=3");
        var afterUpdate = JsonDocument.Parse(await (await client.GetAsync($"/api/search-profiles/{id}")).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(HttpStatusCode.Created, create.StatusCode); Assert.Equal(HttpStatusCode.OK, list.StatusCode); Assert.Equal(HttpStatusCode.OK, update.StatusCode); Assert.Equal("Technology", afterUpdate.GetProperty("preferences").GetProperty("categories")[0].GetString()); Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode); Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/search-profiles/{id}")).StatusCode);
    }
    [Fact]
    public async Task Other_owner_cannot_read_or_mutate_and_stale_version_conflicts()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString); using var a = factory.CreateClient(); using var b = factory.CreateClient(); Authorize(a, factory.CreateToken("search-a")); Authorize(b, factory.CreateToken("search-b"));
        var created = await Send(a, HttpMethod.Post, "/api/search-profiles", Payload("Private", null)); var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var other = await b.GetAsync($"/api/search-profiles/{id}"); var otherUpdate = await Send(b, HttpMethod.Put, $"/api/search-profiles/{id}", Payload("Attempt", 1)); var otherPatch = await Send(b, HttpMethod.Patch, $"/api/search-profiles/{id}/activation", "{\"expectedVersion\":1,\"isActive\":false}"); var otherDelete = await b.DeleteAsync($"/api/search-profiles/{id}?expectedVersion=1");
        Assert.Equal(HttpStatusCode.OK, (await Send(a, HttpMethod.Put, $"/api/search-profiles/{id}", Payload("Valid", 1))).StatusCode);
        var stale = await Send(a, HttpMethod.Put, $"/api/search-profiles/{id}", Payload("Stale", 1));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); Assert.Equal(HttpStatusCode.NotFound, otherUpdate.StatusCode); Assert.Equal(HttpStatusCode.NotFound, otherPatch.StatusCode); Assert.Equal(HttpStatusCode.NotFound, otherDelete.StatusCode); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }
    [Fact]
    public async Task Concurrent_updates_with_the_same_version_have_one_success_and_one_conflict()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString); using var first = factory.CreateClient(); using var second = factory.CreateClient(); var token = factory.CreateToken("concurrent-search"); Authorize(first, token); Authorize(second, token);
        var created = await Send(first, HttpMethod.Post, "/api/search-profiles", Payload("Initial", null)); var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var updates = await Task.WhenAll(Send(first, HttpMethod.Put, $"/api/search-profiles/{id}", Payload("First", 1)), Send(second, HttpMethod.Put, $"/api/search-profiles/{id}", Payload("Second", 1)));
        Assert.Single(updates, response => response.StatusCode == HttpStatusCode.OK); Assert.Single(updates, response => response.StatusCode == HttpStatusCode.Conflict);
        var current = JsonDocument.Parse(await (await first.GetAsync($"/api/search-profiles/{id}")).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, current.GetProperty("version").GetInt32());
    }
    [Fact]
    public async Task Invalid_payload_is_rejected()
    {
        using var factory = new ApiProfileFactory(fixture.ConnectionString); using var client = factory.CreateClient(); Authorize(client, factory.CreateToken("invalid-search"));
        var response = await Send(client, HttpMethod.Post, "/api/search-profiles", "{\"name\":\"Bad\",\"preferences\":{\"categories\":[],\"targetRoles\":[],\"workModes\":[],\"sourceMarkets\":[\"PORTUGAL\"]}}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    private static void Authorize(HttpClient client, string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string json) => client.SendAsync(new HttpRequestMessage(method, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    private static string Payload(string name, int? version) => $$"""{"name":"{{name}}","preferences":{"categories":["Technology"],"targetRoles":["Software Architect"],"workModes":["remote"],"sourceMarkets":["pt","BR"],"remoteFromCountry":"PT","employmentTypes":["Permanent"],"salary":{"minimum":1000,"maximum":2000,"currency":"EUR","period":"monthly"},"languages":["English"],"skills":["C#"],"excludedKeywords":["intern"],"mandatoryCriteria":["category"],"preferredCriteria":["skill"]}{{(version.HasValue ? $",\"expectedVersion\":{version}" : string.Empty)}}}""";
}
