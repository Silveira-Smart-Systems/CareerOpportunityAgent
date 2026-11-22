using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class DiscoveryGeographyContractsTests
{
    private static readonly CountryCode Portugal = new("PT");
    private static readonly CountryCode Brazil = new("BR");
    private static readonly CountryCode UnitedKingdom = new("GB");

    [Fact]
    public void Unconstrained_is_distinct_from_known_countries_and_unknown_provenance()
    {
        var known = PublicationMarketIntent.KnownCountries([Portugal]);

        Assert.Equal(PublicationMarketIntentKind.Unconstrained, PublicationMarketIntent.Unconstrained.Kind);
        Assert.Empty(PublicationMarketIntent.Unconstrained.Countries);
        Assert.False(PublicationMarketIntent.Unconstrained.HasSameIntent(known));
        Assert.IsType<PublicationMarketProvenance.Unknown>(new PublicationMarketProvenance.Unknown());
        Assert.IsType<PublicationMarketProvenance.Known>(new PublicationMarketProvenance.Known(Portugal));
    }

    [Fact]
    public void Known_countries_are_normalized_immutable_and_compared_by_set_intent()
    {
        var input = new List<CountryCode> { Portugal, Brazil, Portugal };
        var intent = PublicationMarketIntent.KnownCountries(input);
        input.Clear();

        Assert.Equal([Brazil, Portugal], intent.Countries.OrderBy(country => country.Value));
        Assert.True(intent.HasSameIntent(PublicationMarketIntent.KnownCountries([Brazil, Portugal])));
    }

    [Fact]
    public void Invalid_publication_market_combinations_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => PublicationMarketIntent.KnownCountries([]));
    }

    [Fact]
    public void Default_country_is_rejected_at_every_new_country_boundary()
    {
        Assert.Throws<ArgumentException>(() => PublicationMarketIntent.KnownCountries([default(CountryCode)]));
        Assert.Throws<ArgumentException>(() => new PublicationMarketProvenance.Known(default));
        Assert.Throws<ArgumentException>(() => new AdvertisedGeographicScope.Country(default));
        Assert.Throws<ArgumentException>(() => new RemoteWorkEligibilityByCountry([
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(default, RemoteWorkEligibility.Unknown)
        ]));

        var eligibility = new RemoteWorkEligibilityByCountry([]);
        Assert.Throws<ArgumentException>(() => eligibility.For(default));
    }

    [Fact]
    public void Country_europe_and_anywhere_are_distinct_typed_scopes()
    {
        var country = new AdvertisedGeographicScope.Country(Portugal);
        var europe = new AdvertisedGeographicScope.Region(GeographicRegion.Europe);
        var anywhere = new AdvertisedGeographicScope.Anywhere();
        var intent = new DiscoveryGeographyIntent(PublicationMarketIntent.Unconstrained, [country, europe, anywhere, country]);

        Assert.Equal(3, intent.AdvertisedGeographies.Count);
        Assert.Contains(country, intent.AdvertisedGeographies);
        Assert.Contains(europe, intent.AdvertisedGeographies);
        Assert.Contains(anywhere, intent.AdvertisedGeographies);
        Assert.Throws<ArgumentException>(() => new AdvertisedGeographicScope.Region((GeographicRegion)99));
    }

    [Fact]
    public void Geographic_intent_equality_includes_publication_and_scope_but_not_scope_order()
    {
        var first = new DiscoveryGeographyIntent(PublicationMarketIntent.KnownCountries([UnitedKingdom]),
            [new AdvertisedGeographicScope.Region(GeographicRegion.Europe), new AdvertisedGeographicScope.Anywhere()]);
        var same = new DiscoveryGeographyIntent(PublicationMarketIntent.KnownCountries([UnitedKingdom]),
            [new AdvertisedGeographicScope.Anywhere(), new AdvertisedGeographicScope.Region(GeographicRegion.Europe)]);
        var different = new DiscoveryGeographyIntent(PublicationMarketIntent.Unconstrained,
            [new AdvertisedGeographicScope.Anywhere(), new AdvertisedGeographicScope.Region(GeographicRegion.Europe)]);

        Assert.True(first.HasSameIntent(same));
        Assert.False(first.HasSameIntent(different));
        Assert.False(first.Equals(same));
    }

    [Fact]
    public void Publication_market_uses_explicit_intent_comparison_not_collection_reference_equality()
    {
        var first = PublicationMarketIntent.KnownCountries([Portugal, Brazil]);
        var same = PublicationMarketIntent.KnownCountries([Brazil, Portugal, Portugal]);

        Assert.True(first.HasSameIntent(same));
        Assert.False(first.Equals(same));
    }

    [Fact]
    public void Remote_eligibility_is_country_specific_and_missing_evidence_is_unknown()
    {
        var eligibility = new RemoteWorkEligibilityByCountry([
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Portugal, RemoteWorkEligibility.Eligible),
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Brazil, RemoteWorkEligibility.NotEligible),
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(UnitedKingdom, RemoteWorkEligibility.Unknown)
        ]);

        Assert.Equal(RemoteWorkEligibility.Eligible, eligibility.For(Portugal));
        Assert.Equal(RemoteWorkEligibility.NotEligible, eligibility.For(Brazil));
        Assert.Equal(RemoteWorkEligibility.Unknown, eligibility.For(UnitedKingdom));
        Assert.Equal(RemoteWorkEligibility.Unknown, eligibility.For(new CountryCode("DE")));
    }

    [Fact]
    public void Conflicting_or_invalid_remote_eligibility_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new RemoteWorkEligibilityByCountry([
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Portugal, RemoteWorkEligibility.Eligible),
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Portugal, RemoteWorkEligibility.NotEligible)
        ]));
        Assert.Throws<ArgumentException>(() => new RemoteWorkEligibilityByCountry([
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Portugal, (RemoteWorkEligibility)99)
        ]));
    }

    [Fact]
    public void Consistent_remote_eligibility_duplicates_are_normalized_and_compared_explicitly()
    {
        var first = new RemoteWorkEligibilityByCountry([
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Portugal, RemoteWorkEligibility.Eligible),
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Portugal, RemoteWorkEligibility.Eligible),
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Brazil, RemoteWorkEligibility.Unknown)
        ]);
        var same = new RemoteWorkEligibilityByCountry([
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Brazil, RemoteWorkEligibility.Unknown),
            new KeyValuePair<CountryCode, RemoteWorkEligibility>(Portugal, RemoteWorkEligibility.Eligible)
        ]);

        Assert.Equal(2, first.Observations.Count);
        Assert.True(first.HasSameObservations(same));
        Assert.False(first.Equals(same));
    }

    [Fact]
    public void Null_geographic_scope_collection_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DiscoveryGeographyIntent(PublicationMarketIntent.Unconstrained, null!));
    }

    [Fact]
    public void Legacy_search_request_retains_source_market_semantics_and_intent_equality()
    {
        var first = LegacyRequest([Portugal, UnitedKingdom]);
        var same = LegacyRequest([UnitedKingdom, Portugal]);
        var different = LegacyRequest([Brazil]);

        Assert.Equal([UnitedKingdom, Portugal], first.SourceMarkets.OrderBy(country => country.Value));
        Assert.True(first.HasSameIntent(same));
        Assert.False(first.HasSameIntent(different));
    }

    private static SearchRequest LegacyRequest(IEnumerable<CountryCode> markets) => new(
        markets, ["Technology"], ["Architect"], ["Remote"], null,
        [SearchCriterion.TargetRole], []);
}
