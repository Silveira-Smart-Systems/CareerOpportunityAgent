using COA.Application.SearchProfiles;
using COA.Domain.SearchProfiles;
namespace COA.Api.SearchProfiles;
public sealed record JobSearchProfileHttpRequest(string Name, JobSearchProfilePreferencesHttpRequest Preferences, int? ExpectedVersion)
{
    public CreateJobSearchProfileRequest ToCreate() => new(Name, Preferences.ToDomain());
    public UpdateJobSearchProfileRequest ToUpdate() => new(ExpectedVersion ?? throw new ArgumentException("Expected version is required."), Name, Preferences.ToDomain());
}
public sealed record JobSearchProfilePreferencesHttpRequest(
    IReadOnlyList<string> Categories, IReadOnlyList<string> TargetRoles, IReadOnlyList<SearchWorkMode> WorkModes,
    IReadOnlyList<string> SourceMarkets, string? RemoteFromCountry, IReadOnlyList<string>? EmploymentTypes,
    SalaryPreferenceHttpRequest? Salary, IReadOnlyList<string>? Languages, IReadOnlyList<string>? Skills,
    IReadOnlyList<string>? ExcludedKeywords, IReadOnlyList<SearchProfileCriterion>? MandatoryCriteria,
    IReadOnlyList<SearchProfileCriterion>? PreferredCriteria)
{
    public JobSearchProfilePreferences ToDomain() => new(Categories, TargetRoles, WorkModes, SourceMarkets, RemoteFromCountry,
        EmploymentTypes ?? [], Salary?.ToDomain(), Languages ?? [], Skills ?? [], ExcludedKeywords ?? [], MandatoryCriteria ?? [], PreferredCriteria ?? []);
}
public sealed record SalaryPreferenceHttpRequest(decimal? Minimum, decimal? Maximum, string? Currency, SalaryPeriod? Period)
{ public SalaryPreference ToDomain() => new(Minimum, Maximum, Currency, Period); }
public sealed record JobSearchProfileActivationHttpRequest(int ExpectedVersion, bool IsActive);
public sealed record JobSearchProfileHttpResponse(Guid Id, int Version, string Name, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, JobSearchProfilePreferencesHttpRequest Preferences)
{
    public static JobSearchProfileHttpResponse FromDomain(JobSearchProfile profile) => new(profile.Id, profile.Version, profile.Name, profile.IsActive, profile.CreatedAtUtc, profile.UpdatedAtUtc, new(profile.Preferences.Categories, profile.Preferences.TargetRoles, profile.Preferences.WorkModes, profile.Preferences.SourceMarkets, profile.Preferences.RemoteFromCountry, profile.Preferences.EmploymentTypes, profile.Preferences.Salary is null ? null : new(profile.Preferences.Salary.Minimum, profile.Preferences.Salary.Maximum, profile.Preferences.Salary.Currency, profile.Preferences.Salary.Period), profile.Preferences.Languages, profile.Preferences.Skills, profile.Preferences.ExcludedKeywords, profile.Preferences.MandatoryCriteria, profile.Preferences.PreferredCriteria));
}
