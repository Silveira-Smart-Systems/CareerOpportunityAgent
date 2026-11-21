using COA.Application.Opportunity;
using COA.Application.Profile;
using COA.Domain.Matching;
using OpportunityModel = COA.Domain.Opportunity.Opportunity;
using Xunit;

namespace COA.UnitTests.Opportunity;

public sealed class OpportunityUseCasesTests
{
    [Fact]
    public async Task Import_creates_a_valid_opportunity_for_the_supplied_owner()
    {
        var store = new FakeOpportunityStore();
        var owner = Owner();

        var result = await new OpportunityUseCases(store).ImportAsync(
            owner,
            "https://jobs.example.test/roles/123?source=synthetic",
            "Software Engineer",
            "Remote .NET role");

        Assert.True(result.Created);
        Assert.Same(owner, store.LastOwner);
        Assert.Equal("https://jobs.example.test/roles/123", result.Opportunity.CanonicalUrl);
        Assert.Equal("Software Engineer", store.LastAdded?.Title);
        Assert.Equal(55m, result.Assessment.Score);
        Assert.True(result.Assessment.Eligible);
    }

    [Theory]
    [InlineData("not-a-url", "Valid title", "Valid description")]
    [InlineData("https://jobs.example.test/invalid", "", "Valid description")]
    [InlineData("https://jobs.example.test/invalid", "Valid title", "")]
    public async Task Import_rejects_invalid_data_before_storing(string url, string title, string description)
    {
        var store = new FakeOpportunityStore();

        await Assert.ThrowsAsync<ArgumentException>(() => new OpportunityUseCases(store).ImportAsync(
            Owner(), url, title, description));

        Assert.Null(store.LastAdded);
    }

    [Fact]
    public async Task Ranking_is_deterministic_and_unknown_criteria_remain_not_proven()
    {
        var highMatch = OpportunityModel.Create(
            "https://jobs.example.test/high-match",
            "High match",
            "A remote .NET role");
        var lowMatch = OpportunityModel.Create(
            "https://jobs.example.test/low-match",
            "Low match",
            "A role with no verified requirements");
        var useCases = new OpportunityUseCases(new FakeOpportunityStore
        {
            Opportunities = [lowMatch, highMatch]
        });

        var first = await useCases.ListScoredAsync(Owner());
        var second = await useCases.ListScoredAsync(Owner());

        Assert.Equal(["High match", "Low match"], first.Select(item => item.Opportunity.Title));
        Assert.Equal(
            first.Select(item => (item.Opportunity.Id, item.Assessment.Score, item.Assessment.Eligible)),
            second.Select(item => (item.Opportunity.Id, item.Assessment.Score, item.Assessment.Eligible)));
        Assert.Equal(55m, first[0].Assessment.Score);
        Assert.True(first[0].Assessment.Eligible);
        Assert.Equal(0m, first[1].Assessment.Score);
        Assert.False(first[1].Assessment.Eligible);
        Assert.All(
            first[0].Assessment.Criteria.Where(item => item.Code is "commercial" or "seniority" or "career-alignment"),
            item => Assert.Equal(EvidenceState.UnknownNotProven, item.Evidence));
    }

    private static OwnerIdentity Owner() => new("https://issuer.example", $"synthetic-{Guid.NewGuid():N}");

    private sealed class FakeOpportunityStore : IOpportunityStore
    {
        public OpportunityModel? LastAdded { get; private set; }

        public OwnerIdentity? LastOwner { get; private set; }

        public IReadOnlyList<OpportunityModel> Opportunities { get; init; } = [];

        public Task<bool> AddAsync(OwnerIdentity owner, OpportunityModel opportunity, CancellationToken ct = default)
        {
            LastOwner = owner;
            LastAdded = opportunity;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<OpportunityModel>> ListAsync(OwnerIdentity owner, CancellationToken ct = default)
        {
            LastOwner = owner;
            return Task.FromResult(Opportunities);
        }
    }
}
