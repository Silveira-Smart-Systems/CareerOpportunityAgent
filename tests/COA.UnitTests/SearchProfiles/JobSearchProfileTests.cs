using COA.Domain.SearchProfiles;
using Xunit;
namespace COA.UnitTests.SearchProfiles;
public sealed class JobSearchProfileTests
{
    [Fact]
    public void Create_normalizes_values_and_starts_active()
    {
        var profile = JobSearchProfile.Create(Guid.NewGuid(), "  Career focus ", Preferences(categories: [" Technology ", "technology"], roles: [" Technical Lead ", "technical lead"], markets: ["pt", "PT"]), DateTime.UtcNow);
        Assert.Equal("Career focus", profile.Name); Assert.True(profile.IsActive); Assert.Equal(["Technology"], profile.Preferences.Categories); Assert.Equal(["PT"], profile.Preferences.SourceMarkets);
    }
    [Theory]
    [InlineData("P")]
    [InlineData("PRT")]
    [InlineData("1T")]
    public void Invalid_country_is_rejected(string market) => Assert.Throws<ArgumentException>(() => JobSearchProfile.Create(Guid.NewGuid(), "Profile", Preferences(markets: [market]), DateTime.UtcNow));
    [Fact]
    public void Overlapping_criteria_are_rejected() => Assert.Throws<ArgumentException>(() => JobSearchProfile.Create(Guid.NewGuid(), "Profile", Preferences(mandatory: [SearchProfileCriterion.Skill], preferred: [SearchProfileCriterion.Skill]), DateTime.UtcNow));
    [Fact]
    public void Invalid_work_mode_is_rejected() => Assert.Throws<ArgumentException>(() => JobSearchProfile.Create(Guid.NewGuid(), "Profile", new JobSearchProfilePreferences(["Technology"], ["Architect"], [(SearchWorkMode)99], ["PT"], null, [], null, [], [], [], [], []), DateTime.UtcNow));
    [Fact]
    public void Non_technology_roles_follow_the_same_rules()
    {
        var profile = JobSearchProfile.Create(Guid.NewGuid(), "Clinical", Preferences(categories: ["Healthcare"], roles: ["Registered Nurse"], markets: ["BR"]), DateTime.UtcNow);
        Assert.Equal("Registered Nurse", profile.Preferences.TargetRoles.Single());
    }
    private static JobSearchProfilePreferences Preferences(IReadOnlyList<string>? categories = null, IReadOnlyList<string>? roles = null, IReadOnlyList<string>? markets = null, IReadOnlyList<SearchProfileCriterion>? mandatory = null, IReadOnlyList<SearchProfileCriterion>? preferred = null) =>
        new(categories ?? ["Technology"], roles ?? ["Software Architect"], [SearchWorkMode.Remote], markets ?? ["PT"], "pt", [], new SalaryPreference(10m, 20m, "EUR", SalaryPeriod.Hourly), ["English"], ["C#"], ["intern"], mandatory ?? [], preferred ?? []);
}
