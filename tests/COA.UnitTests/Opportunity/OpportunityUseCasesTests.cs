using COA.Application.Opportunity;
using COA.Application.Profile;
using COA.Application.SearchProfiles;
using COA.Domain.Matching;
using COA.Domain.Profile;
using COA.Domain.SearchProfiles;
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

    [Fact]
    public async Task Contextual_matching_uses_proven_profile_evidence_once_per_list()
    {
        var owner = Owner();
        var opportunities = new FakeOpportunityStore { Opportunities = [OpportunityModel.Create("https://jobs.example.test/context", "Architect", "dotnet remote role"), OpportunityModel.Create("https://jobs.example.test/unknown", "Other", "unknown")] };
        var profiles = new RecordingProfileStore(CareerProfile.Create(Guid.NewGuid(), "Architect", [new ProfileEvidence("dotnet", "fixture", EvidenceOrigin.UserProvided)], new CareerPreferences(WorkArrangement.Remote, true, null, null, 0)));
        var searches = new RecordingSearchStore();
        var result = await new OpportunityUseCases(opportunities, profiles, searches).ListScoredAsync(owner);
        Assert.Equal(1, profiles.Reads); Assert.Equal(1, searches.Reads);
        Assert.Equal(EvidenceState.Matched, result[0].Assessment.Criteria.Single(x => x.Code == "technical").Evidence);
        Assert.Equal(EvidenceState.UnknownNotProven, result[0].Assessment.Criteria.Single(x => x.Code == "work-model").Evidence);
        Assert.False(result[0].Assessment.Eligible); // remote-only is a hard constraint and has no proven job evidence.
        Assert.Equal(EvidenceState.UnknownNotProven, result[1].Assessment.Criteria.Single(x => x.Code == "technical").Evidence);
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
    private sealed class RecordingProfileStore(CareerProfile profile) : IProfileStore { public int Reads; public Task<CareerProfile?> GetCurrentAsync(OwnerIdentity owner,CancellationToken ct=default){Reads++;return Task.FromResult<CareerProfile?>(profile);} public Task<CreateProfileResult> CreateAsync(OwnerIdentity owner,CareerProfile profile,CancellationToken ct=default)=>throw new NotSupportedException(); public Task<CareerProfile?> GetVersionAsync(OwnerIdentity owner,int version,CancellationToken ct=default)=>throw new NotSupportedException(); public Task<ReviseProfileResult> ReviseAsync(OwnerIdentity owner,int version,CareerProfile profile,CancellationToken ct=default)=>throw new NotSupportedException(); }
    private sealed class RecordingSearchStore : IJobSearchProfileStore { public int Reads; public Task<IReadOnlyList<JobSearchProfile>> ListAsync(OwnerIdentity owner,CancellationToken ct=default){Reads++;return Task.FromResult<IReadOnlyList<JobSearchProfile>>([]);} public Task<JobSearchProfile> CreateAsync(OwnerIdentity owner,JobSearchProfile profile,CancellationToken ct=default)=>throw new NotSupportedException(); public Task<JobSearchProfile?> GetAsync(OwnerIdentity owner,Guid id,CancellationToken ct=default)=>throw new NotSupportedException(); public Task<SearchProfileMutationResult> UpdateAsync(OwnerIdentity owner,Guid id,int v,Func<JobSearchProfile,JobSearchProfile> m,CancellationToken ct=default)=>throw new NotSupportedException(); public Task<SearchProfileMutationResult> DeleteAsync(OwnerIdentity owner,int v,Guid id,CancellationToken ct=default)=>throw new NotSupportedException(); }
}
