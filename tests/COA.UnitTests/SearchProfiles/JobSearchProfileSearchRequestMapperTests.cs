using COA.Application.Discovery;
using COA.Application.SearchProfiles;
using COA.Domain.SearchProfiles;
using Xunit;

namespace COA.UnitTests.SearchProfiles;

public sealed class JobSearchProfileSearchRequestMapperTests
{
    private readonly JobSearchProfileSearchRequestMapper _mapper = new();

    [Fact]
    public void Active_profile_maps_all_typed_filters_and_priorities_deterministically()
    {
        var profile = Profile(
            categories: [" Technology "], roles: [" Software Architect "], markets: ["gb", "PT", "BR"],
            mandatory: [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole, SearchProfileCriterion.RemoteFromCountry,
                SearchProfileCriterion.EmploymentType, SearchProfileCriterion.Salary, SearchProfileCriterion.Skill],
            preferred: [SearchProfileCriterion.WorkMode, SearchProfileCriterion.Language, SearchProfileCriterion.ExcludedKeyword]);

        var first = Assert.IsType<MappedSearchRequest>(_mapper.Map(profile)).SearchRequest;
        var second = Assert.IsType<MappedSearchRequest>(_mapper.Map(profile)).SearchRequest;

        Assert.True(first.HasSameIntent(second));
        Assert.Equal(["BR", "GB", "PT"], first.SourceMarkets.Select(market => market.Value).OrderBy(value => value));
        Assert.Equal(["Permanent"], first.EmploymentTypes);
        Assert.Equal(new SalaryPreference(1000m, 2000m, "EUR", SalaryPeriod.Monthly), first.SalaryPreference);
        Assert.Equal(["English"], first.Languages); Assert.Equal(["C#"], first.Skills); Assert.Equal(["intern"], first.ExcludedKeywords);
        Assert.Equal(new CountryCode("PT"), first.RemoteFromCountry);
        Assert.Contains(SearchCriterion.Contract, first.MandatoryCriteria);
        Assert.Contains(SearchCriterion.ExcludedKeyword, first.PreferredCriteria);
    }

    [Fact]
    public void Inactive_profile_has_no_executable_request()
    {
        var profile = Profile().SetActive(false, DateTime.UtcNow);

        Assert.IsType<InactiveProfile>(_mapper.Map(profile));
    }

    [Fact]
    public void Filled_unclassified_family_returns_incomplete_profile_without_request()
    {
        var profile = Profile(
            mandatory: [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole, SearchProfileCriterion.WorkMode,
                SearchProfileCriterion.RemoteFromCountry, SearchProfileCriterion.EmploymentType, SearchProfileCriterion.Salary,
                SearchProfileCriterion.Language, SearchProfileCriterion.ExcludedKeyword]);

        var result = Assert.IsType<IncompleteProfile>(_mapper.Map(profile));

        Assert.Equal([SearchCriterion.Skill], result.UnclassifiedCriteria);
    }

    [Fact]
    public void Multiple_filled_unclassified_families_are_reported_together()
    {
        var profile = Profile(mandatory: [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole]);

        var result = Assert.IsType<IncompleteProfile>(_mapper.Map(profile));

        Assert.True(result.UnclassifiedCriteria.SetEquals([
            SearchCriterion.WorkMode, SearchCriterion.RemoteFromCountry, SearchCriterion.Contract,
            SearchCriterion.Salary, SearchCriterion.Language, SearchCriterion.Skill, SearchCriterion.ExcludedKeyword]));
    }

    [Fact]
    public void Optional_absence_is_not_incomplete_and_non_technology_profile_maps()
    {
        var profile = JobSearchProfile.Create(Guid.NewGuid(), "Clinical", new JobSearchProfilePreferences(
            ["Healthcare"], ["Registered Nurse"], [], ["BR"], null, [], null, [], [], [],
            [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole], []), DateTime.UtcNow);

        var request = Assert.IsType<MappedSearchRequest>(_mapper.Map(profile)).SearchRequest;

        Assert.Equal(["Healthcare"], request.Categories); Assert.Equal(["Registered Nurse"], request.TargetRoles);
        Assert.Empty(request.Skills); Assert.Null(request.RemoteFromCountry);
    }

    private static JobSearchProfile Profile(
        IReadOnlyList<string>? categories = null, IReadOnlyList<string>? roles = null, IReadOnlyList<string>? markets = null,
        IReadOnlyList<SearchProfileCriterion>? mandatory = null, IReadOnlyList<SearchProfileCriterion>? preferred = null) =>
        JobSearchProfile.Create(Guid.NewGuid(), "Career", new JobSearchProfilePreferences(
            categories ?? ["Technology"], roles ?? ["Architect"], [SearchWorkMode.Remote], markets ?? ["GB"], "PT",
            ["Permanent"], new SalaryPreference(1000m, 2000m, "EUR", SalaryPeriod.Monthly), ["English"], ["C#"], ["intern"],
            mandatory ?? [SearchProfileCriterion.Category, SearchProfileCriterion.TargetRole, SearchProfileCriterion.WorkMode,
                SearchProfileCriterion.RemoteFromCountry, SearchProfileCriterion.EmploymentType, SearchProfileCriterion.Salary,
                SearchProfileCriterion.Language, SearchProfileCriterion.Skill, SearchProfileCriterion.ExcludedKeyword],
            preferred ?? []), DateTime.UtcNow);
}
