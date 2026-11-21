using System.Collections.Frozen;
using COA.Application.Discovery;
using COA.Domain.SearchProfiles;

namespace COA.Application.SearchProfiles;

public abstract record ProfileSearchRequestMappingResult;

public sealed record MappedSearchRequest : ProfileSearchRequestMappingResult
{
    public MappedSearchRequest(SearchRequest searchRequest) =>
        SearchRequest = searchRequest ?? throw new ArgumentNullException(nameof(searchRequest));

    public SearchRequest SearchRequest { get; }
}

public sealed record InactiveProfile : ProfileSearchRequestMappingResult;

public sealed record IncompleteProfile : ProfileSearchRequestMappingResult
{
    public IncompleteProfile(IEnumerable<SearchCriterion> unclassifiedCriteria)
    {
        UnclassifiedCriteria = (unclassifiedCriteria ?? throw new ArgumentNullException(nameof(unclassifiedCriteria)))
            .ToFrozenSet();
        if (UnclassifiedCriteria.Count == 0)
            throw new ArgumentException("An incomplete profile requires unclassified criteria.", nameof(unclassifiedCriteria));
    }

    public IReadOnlySet<SearchCriterion> UnclassifiedCriteria { get; }
}

public sealed class JobSearchProfileSearchRequestMapper
{
    public ProfileSearchRequestMappingResult Map(JobSearchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if (!profile.IsActive) return new InactiveProfile();

        var preferences = profile.Preferences;
        var mandatory = MapCriteria(preferences.MandatoryCriteria);
        var preferred = MapCriteria(preferences.PreferredCriteria);
        var unclassified = EffectiveCriteria(preferences)
            .Except(mandatory)
            .Except(preferred)
            .ToArray();
        if (unclassified.Length > 0) return new IncompleteProfile(unclassified);

        return new MappedSearchRequest(new SearchRequest(
            preferences.SourceMarkets.Select(country => new CountryCode(country)),
            preferences.Categories,
            preferences.TargetRoles,
            preferences.WorkModes.Select(mode => mode.ToString()),
            preferences.RemoteFromCountry is null ? null : new CountryCode(preferences.RemoteFromCountry),
            preferences.EmploymentTypes,
            preferences.Salary,
            preferences.Languages,
            preferences.Skills,
            preferences.ExcludedKeywords,
            mandatory,
            preferred));
    }

    private static IEnumerable<SearchCriterion> EffectiveCriteria(JobSearchProfilePreferences preferences)
    {
        yield return SearchCriterion.Category;
        yield return SearchCriterion.TargetRole;
        if (preferences.WorkModes.Count > 0) yield return SearchCriterion.WorkMode;
        if (preferences.RemoteFromCountry is not null) yield return SearchCriterion.RemoteFromCountry;
        if (preferences.EmploymentTypes.Count > 0) yield return SearchCriterion.Contract;
        if (preferences.Salary is not null) yield return SearchCriterion.Salary;
        if (preferences.Languages.Count > 0) yield return SearchCriterion.Language;
        if (preferences.Skills.Count > 0) yield return SearchCriterion.Skill;
        if (preferences.ExcludedKeywords.Count > 0) yield return SearchCriterion.ExcludedKeyword;
    }

    private static IReadOnlySet<SearchCriterion> MapCriteria(IEnumerable<SearchProfileCriterion> criteria) =>
        criteria.Select(criterion => criterion switch
        {
            SearchProfileCriterion.Category => SearchCriterion.Category,
            SearchProfileCriterion.TargetRole => SearchCriterion.TargetRole,
            SearchProfileCriterion.WorkMode => SearchCriterion.WorkMode,
            SearchProfileCriterion.RemoteFromCountry => SearchCriterion.RemoteFromCountry,
            SearchProfileCriterion.EmploymentType => SearchCriterion.Contract,
            SearchProfileCriterion.Salary => SearchCriterion.Salary,
            SearchProfileCriterion.Language => SearchCriterion.Language,
            SearchProfileCriterion.Skill => SearchCriterion.Skill,
            SearchProfileCriterion.ExcludedKeyword => SearchCriterion.ExcludedKeyword,
            _ => throw new ArgumentOutOfRangeException(nameof(criteria))
        }).ToFrozenSet();
}
