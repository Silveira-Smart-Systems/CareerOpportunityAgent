using System.Collections.Frozen;

namespace COA.Application.Discovery;

public enum PublicationMarketIntentKind
{
    Unconstrained,
    KnownCountries
}

/// <summary>
/// A search constraint over a publication market. It is intentionally distinct from both
/// advertised geography and absent/unknown source provenance.
/// </summary>
public sealed class PublicationMarketIntent
{
    private PublicationMarketIntent(PublicationMarketIntentKind kind, IEnumerable<CountryCode> countries)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Publication market intent kind is invalid.", nameof(kind));
        var copied = (countries ?? throw new ArgumentNullException(nameof(countries))).ToArray();
        foreach (var country in copied) DiscoveryGeographyValidation.ValidateCountry(country, nameof(countries));
        Countries = copied.ToFrozenSet();
        if (kind == PublicationMarketIntentKind.Unconstrained && Countries.Count != 0)
            throw new ArgumentException("An unconstrained publication market cannot contain countries.", nameof(countries));
        if (kind == PublicationMarketIntentKind.KnownCountries && Countries.Count == 0)
            throw new ArgumentException("A known publication market requires at least one country.", nameof(countries));
        Kind = kind;
    }

    public static PublicationMarketIntent Unconstrained { get; } = new(PublicationMarketIntentKind.Unconstrained, []);
    public PublicationMarketIntentKind Kind { get; }
    public IReadOnlySet<CountryCode> Countries { get; }

    public static PublicationMarketIntent KnownCountries(IEnumerable<CountryCode> countries) =>
        new(PublicationMarketIntentKind.KnownCountries, countries);

    public bool HasSameIntent(PublicationMarketIntent other) => other is not null &&
        Kind == other.Kind && Countries.SetEquals(other.Countries);
}

/// <summary>What the source has evidenced about a candidate's publication market.</summary>
public abstract record PublicationMarketProvenance
{
    private PublicationMarketProvenance() { }
    public sealed record Known : PublicationMarketProvenance
    {
        public Known(CountryCode country)
        {
            DiscoveryGeographyValidation.ValidateCountry(country, nameof(country));
            Country = country;
        }

        public CountryCode Country { get; }
    }
    public sealed record Unknown : PublicationMarketProvenance;
}

public enum GeographicRegion
{
    Europe
}

/// <summary>
/// A provider-neutral advertised/search geography. It is not a source publication market and
/// says nothing about remote-work eligibility.
/// </summary>
public abstract record AdvertisedGeographicScope
{
    private AdvertisedGeographicScope() { }

    public sealed record Country : AdvertisedGeographicScope
    {
        public Country(CountryCode value)
        {
            DiscoveryGeographyValidation.ValidateCountry(value, nameof(value));
            Value = value;
        }
        public CountryCode Value { get; }
    }

    public sealed record Region : AdvertisedGeographicScope
    {
        public Region(GeographicRegion value)
        {
            if (!Enum.IsDefined(value)) throw new ArgumentException("Geographic region is invalid.", nameof(value));
            Value = value;
        }

        public GeographicRegion Value { get; }
    }

    public sealed record Anywhere : AdvertisedGeographicScope;
}

public enum RemoteWorkEligibility
{
    Eligible,
    NotEligible,
    Unknown
}

/// <summary>
/// Evidence of remote-work eligibility is country-specific. A missing country is Unknown, never
/// an implicit Eligible or NotEligible result.
/// </summary>
public sealed class RemoteWorkEligibilityByCountry
{
    private readonly IReadOnlyDictionary<CountryCode, RemoteWorkEligibility> _observations;

    public RemoteWorkEligibilityByCountry(IEnumerable<KeyValuePair<CountryCode, RemoteWorkEligibility>> observations)
    {
        var copied = (observations ?? throw new ArgumentNullException(nameof(observations))).ToArray();
        if (copied.Any(observation => !Enum.IsDefined(observation.Value)))
            throw new ArgumentException("Remote work eligibility contains an invalid value.", nameof(observations));
        foreach (var observation in copied) DiscoveryGeographyValidation.ValidateCountry(observation.Key, nameof(observations));
        if (copied.GroupBy(observation => observation.Key).Any(group => group.Select(x => x.Value).Distinct().Count() > 1))
            throw new ArgumentException("A country cannot have conflicting remote work eligibility.", nameof(observations));
        _observations = copied
            .GroupBy(observation => observation.Key)
            .ToFrozenDictionary(group => group.Key, group => group.First().Value);
    }

    public IReadOnlyDictionary<CountryCode, RemoteWorkEligibility> Observations => _observations;

    public RemoteWorkEligibility For(CountryCode country)
    {
        DiscoveryGeographyValidation.ValidateCountry(country, nameof(country));
        return _observations.GetValueOrDefault(country, RemoteWorkEligibility.Unknown);
    }

    public bool HasSameObservations(RemoteWorkEligibilityByCountry other) => other is not null &&
        _observations.Count == other._observations.Count &&
        _observations.All(observation => other._observations.TryGetValue(observation.Key, out var value) && value == observation.Value);
}

/// <summary>
/// New, provider-neutral geographic intent. It is deliberately separate from legacy
/// SearchRequest.SourceMarkets until Planner/Orchestrator are evolved in a later phase.
/// </summary>
public sealed class DiscoveryGeographyIntent
{
    public DiscoveryGeographyIntent(PublicationMarketIntent publicationMarket,
        IEnumerable<AdvertisedGeographicScope> advertisedGeographies)
    {
        PublicationMarket = publicationMarket ?? throw new ArgumentNullException(nameof(publicationMarket));
        var copied = (advertisedGeographies ?? throw new ArgumentNullException(nameof(advertisedGeographies))).ToArray();
        if (copied.Any(scope => scope is null))
            throw new ArgumentException("Advertised geographic scopes cannot contain null.", nameof(advertisedGeographies));
        AdvertisedGeographies = copied.ToFrozenSet();
    }

    public PublicationMarketIntent PublicationMarket { get; }
    public IReadOnlySet<AdvertisedGeographicScope> AdvertisedGeographies { get; }

    public bool HasSameIntent(DiscoveryGeographyIntent other) => other is not null &&
        PublicationMarket.HasSameIntent(other.PublicationMarket) &&
        AdvertisedGeographies.SetEquals(other.AdvertisedGeographies);
}

internal static class DiscoveryGeographyValidation
{
    internal static void ValidateCountry(CountryCode country, string parameterName)
    {
        var value = country.Value;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 2 || !value.All(char.IsAsciiLetter))
            throw new ArgumentException("A valid ISO 3166-1 alpha-2 country code is required.", parameterName);
    }
}
