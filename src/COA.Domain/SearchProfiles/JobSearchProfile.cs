namespace COA.Domain.SearchProfiles;

public enum SearchWorkMode { Remote, Hybrid, Onsite }
public enum SearchProfileCriterion { Category, TargetRole, WorkMode, RemoteFromCountry, EmploymentType, Salary, Language, Skill, ExcludedKeyword }
public enum SalaryPeriod { Hourly, Monthly, Yearly }

public sealed record SalaryPreference(decimal? Minimum, decimal? Maximum, string? Currency, SalaryPeriod? Period)
{
    public void Validate()
    {
        if (Minimum is < 0 or > 100000000 || Maximum is < 0 or > 100000000 ||
            (Minimum.HasValue && Maximum.HasValue && Minimum > Maximum))
            throw new ArgumentException("Salary range is invalid.");
        if ((Minimum.HasValue || Maximum.HasValue) &&
            (string.IsNullOrWhiteSpace(Currency) || Currency.Length != 3 || !Currency.All(char.IsLetter) || Period is null))
            throw new ArgumentException("Currency and period are required with a salary range.");
        if (!Minimum.HasValue && !Maximum.HasValue && (Currency is not null || Period is not null))
            throw new ArgumentException("Salary range is required with currency or period.");
        if (Period.HasValue && !Enum.IsDefined(Period.Value))
            throw new ArgumentException("Salary period is invalid.");
        if (Minimum.HasValue && Minimum.Value % .01m != 0 || Maximum.HasValue && Maximum.Value % .01m != 0)
            throw new ArgumentException("Salary supports at most two decimal places.");
    }
}

public sealed record JobSearchProfilePreferences(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> TargetRoles,
    IReadOnlyList<SearchWorkMode> WorkModes,
    IReadOnlyList<string> SourceMarkets,
    string? RemoteFromCountry,
    IReadOnlyList<string> EmploymentTypes,
    SalaryPreference? Salary,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> ExcludedKeywords,
    IReadOnlyList<SearchProfileCriterion> MandatoryCriteria,
    IReadOnlyList<SearchProfileCriterion> PreferredCriteria)
{
    public JobSearchProfilePreferences NormalizeAndValidate()
    {
        var result = this with
        {
            Categories = NormalizeText(Categories, 100, nameof(Categories)),
            TargetRoles = NormalizeText(TargetRoles, 200, nameof(TargetRoles)),
            WorkModes = NormalizeModes(WorkModes),
            SourceMarkets = NormalizeCountries(SourceMarkets, nameof(SourceMarkets)),
            RemoteFromCountry = NormalizeCountry(RemoteFromCountry, nameof(RemoteFromCountry)),
            EmploymentTypes = NormalizeText(EmploymentTypes, 100, nameof(EmploymentTypes)),
            Languages = NormalizeText(Languages, 100, nameof(Languages)),
            Skills = NormalizeText(Skills, 200, nameof(Skills)),
            ExcludedKeywords = NormalizeText(ExcludedKeywords, 200, nameof(ExcludedKeywords)),
            MandatoryCriteria = NormalizeCriteria(MandatoryCriteria, nameof(MandatoryCriteria)),
            PreferredCriteria = NormalizeCriteria(PreferredCriteria, nameof(PreferredCriteria))
        };
        if (result.Categories.Count == 0 || result.TargetRoles.Count == 0 || result.SourceMarkets.Count == 0)
            throw new ArgumentException("At least one category, target role, and source market is required.");
        if (result.MandatoryCriteria.Intersect(result.PreferredCriteria).Any())
            throw new ArgumentException("A criterion cannot be both mandatory and preferred.");
        result.Salary?.Validate();
        return result;
    }

    private static IReadOnlyList<string> NormalizeText(IReadOnlyList<string>? values, int maximumLength, string name) =>
        (values ?? throw new ArgumentNullException(name)).Select(value =>
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
                throw new ArgumentException("Search value is invalid.", name);
            return value.Trim();
        }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    private static IReadOnlyList<string> NormalizeCountries(IReadOnlyList<string>? values, string name) =>
        (values ?? throw new ArgumentNullException(name)).Select(value => NormalizeCountry(value, name)!).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static string? NormalizeCountry(string? value, string name)
    {
        if (value is null) return null;
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 2 || !normalized.All(char.IsAsciiLetter))
            throw new ArgumentException("An ISO 3166-1 alpha-2 country code is required.", name);
        return normalized;
    }

    private static IReadOnlyList<SearchWorkMode> NormalizeModes(IReadOnlyList<SearchWorkMode>? values) =>
        (values ?? throw new ArgumentNullException(nameof(WorkModes))).Distinct().OrderBy(value => value).ToArray();
    private static IReadOnlyList<SearchProfileCriterion> NormalizeCriteria(IReadOnlyList<SearchProfileCriterion>? values, string name)
    {
        var result = (values ?? throw new ArgumentNullException(name)).Distinct().OrderBy(value => value).ToArray();
        if (result.Any(value => !Enum.IsDefined(value))) throw new ArgumentException("Search criterion is invalid.", name);
        return result;
    }
}

public sealed record JobSearchProfile(
    Guid Id, int Version, string Name, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    JobSearchProfilePreferences Preferences)
{
    public static JobSearchProfile Create(Guid id, string name, JobSearchProfilePreferences preferences, DateTime utcNow)
    {
        var profile = new JobSearchProfile(id, 1, NormalizeName(name), true, utcNow, utcNow, preferences.NormalizeAndValidate());
        profile.Validate(); return profile;
    }
    public JobSearchProfile Revise(string name, JobSearchProfilePreferences preferences, DateTime utcNow) =>
        new(Id, checked(Version + 1), NormalizeName(name), IsActive, CreatedAtUtc, utcNow, preferences.NormalizeAndValidate());
    public JobSearchProfile SetActive(bool isActive, DateTime utcNow) => this with { Version = checked(Version + 1), IsActive = isActive, UpdatedAtUtc = utcNow };
    public void Validate()
    {
        if (Id == Guid.Empty || Version < 1 || string.IsNullOrWhiteSpace(Name) || Name.Length > 120 || CreatedAtUtc.Kind != DateTimeKind.Utc || UpdatedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Job search profile is invalid.");
        Preferences.NormalizeAndValidate();
    }
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new ArgumentException("Profile name is required and limited to 120 characters.", nameof(name));
        return name.Trim();
    }
}
