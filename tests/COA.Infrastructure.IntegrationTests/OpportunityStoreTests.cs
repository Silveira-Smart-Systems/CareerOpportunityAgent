using COA.Application.Profile;
using COA.Domain.Opportunity;
using Xunit;

namespace COA.Infrastructure.IntegrationTests;

public sealed class OpportunityStoreTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Same_owner_and_canonical_url_are_deduplicated_but_other_owners_are_isolated()
    {
        var ownerA = Owner("owner-a");
        var ownerB = Owner("owner-b");
        var first = Opportunity.Create("https://jobs.example.test/roles/42?source=first", "First", "Synthetic role");
        var duplicate = Opportunity.Create("https://jobs.example.test/roles/42?source=second", "Duplicate", "Synthetic role");
        var otherOwner = Opportunity.Create("https://jobs.example.test/roles/42", "Other owner", "Synthetic role");
        var store = fixture.CreateOpportunityStore();

        var created = await store.AddAsync(ownerA, first);
        var duplicateCreated = await store.AddAsync(ownerA, duplicate);
        var otherOwnerCreated = await store.AddAsync(ownerB, otherOwner);

        Assert.True(created);
        Assert.False(duplicateCreated);
        Assert.True(otherOwnerCreated);
        Assert.Equal([first.Id], (await store.ListAsync(ownerA)).Select(item => item.Id));
        Assert.Equal([otherOwner.Id], (await store.ListAsync(ownerB)).Select(item => item.Id));
    }

    [Fact]
    public async Task Concurrent_imports_of_the_same_owner_and_canonical_url_return_one_conflict()
    {
        var owner = Owner("concurrent");
        var first = Opportunity.Create("https://jobs.example.test/roles/concurrent?source=first", "First", "Synthetic role");
        var second = Opportunity.Create("https://jobs.example.test/roles/concurrent?source=second", "Second", "Synthetic role");

        var results = await Task.WhenAll(
            fixture.CreateOpportunityStore().AddAsync(owner, first),
            fixture.CreateOpportunityStore().AddAsync(owner, second));

        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        Assert.Single(await fixture.CreateOpportunityStore().ListAsync(owner));
    }

    [Fact]
    public async Task Synthetic_provenance_round_trips_and_legacy_default_is_false()
    {
        var owner = Owner("provenance");
        var legacy = Opportunity.Create("https://jobs.example.test/legacy", "Legacy", "Synthetic role");
        var demo = Opportunity.Create("https://jobs.example.test/demo", "Demo", "Synthetic role", isSynthetic: true);
        var store = fixture.CreateOpportunityStore();

        Assert.True(await store.AddAsync(owner, legacy));
        Assert.True(await store.AddAsync(owner, demo));

        var persisted = await store.ListAsync(owner);
        Assert.False(persisted.Single(item => item.Id == legacy.Id).IsSynthetic);
        Assert.True(persisted.Single(item => item.Id == demo.Id).IsSynthetic);
    }

    private static OwnerIdentity Owner(string suffix) =>
        new("https://issuer.example", $"synthetic-opportunity-{suffix}-{Guid.NewGuid():N}");
}
