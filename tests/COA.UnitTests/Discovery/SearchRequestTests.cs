using COA.Application.Discovery;
using COA.Domain.SearchProfiles;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class SearchRequestTests
{
    private static readonly CountryCode Gb = new("GB");

    [Fact]
    public void Legacy_constructor_keeps_new_filters_empty()
    {
        var request = new SearchRequest([Gb], ["Technology"], ["Architect"], ["Remote"], null, [SearchCriterion.TargetRole], []);

        Assert.Empty(request.EmploymentTypes); Assert.Null(request.SalaryPreference); Assert.Empty(request.Languages);
        Assert.Empty(request.Skills); Assert.Empty(request.ExcludedKeywords);
    }

    [Fact]
    public void Typed_filters_are_normalized_immutable_and_part_of_intent()
    {
        var employment = new List<string> { " Permanent ", "permanent" };
        var request = NewRequest(employmentTypes: employment, skills: [" C# " ]);
        var changed = NewRequest(employmentTypes: ["Contract"], skills: ["C#"]);
        employment.Clear();

        Assert.Equal(["Permanent"], request.EmploymentTypes); Assert.Equal(["C#"], request.Skills);
        Assert.False(request.HasSameIntent(changed));
    }

    [Fact]
    public void Salary_is_validated_and_part_of_intent()
    {
        var request = NewRequest(salary: new SalaryPreference(1000m, null, "EUR", SalaryPeriod.Monthly));
        var changed = NewRequest(salary: new SalaryPreference(2000m, null, "EUR", SalaryPeriod.Monthly));

        Assert.False(request.HasSameIntent(changed));
        Assert.Throws<ArgumentException>(() => NewRequest(salary: new SalaryPreference(10m, 1m, "EUR", SalaryPeriod.Monthly)));
    }

    [Fact]
    public void Language_skill_and_excluded_keyword_are_each_part_of_intent()
    {
        var baseline = NewRequest(languages: ["English"], skills: ["C#"], excludedKeywords: ["intern"]);

        Assert.False(baseline.HasSameIntent(NewRequest(languages: ["Portuguese"], skills: ["C#"], excludedKeywords: ["intern"])));
        Assert.False(baseline.HasSameIntent(NewRequest(languages: ["English"], skills: ["Azure"], excludedKeywords: ["intern"])));
        Assert.False(baseline.HasSameIntent(NewRequest(languages: ["English"], skills: ["C#"], excludedKeywords: ["junior"])));
    }

    private static SearchRequest NewRequest(IEnumerable<string>? employmentTypes = null, SalaryPreference? salary = null,
        IEnumerable<string>? languages = null, IEnumerable<string>? skills = null, IEnumerable<string>? excludedKeywords = null) =>
        new([Gb], ["Technology"], ["Architect"], ["Remote"], new CountryCode("PT"), employmentTypes ?? [], salary,
            languages ?? ["English"], skills ?? [], excludedKeywords ?? ["intern"], [SearchCriterion.TargetRole], []);
}
