using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedGeographyClaimEvaluatorTests
{
    [Fact]
    public void Conflicting_provider_country_is_reported_without_mutating_requested_scope()
    {
        var scope = new SourceExecutionScope.PublicationCountry(new CountryCode("PT"));
        var claim = new ScopedSourceGeographyClaim(
            new ObservedPublicationGeography.Countries([new CountryCode("ES")]));
        Assert.Equal(ScopedGeographyClaimAssessment.ConflictsWithRequestedCountry,
            ScopedGeographyClaimEvaluator.Evaluate(scope, claim));
        Assert.Equal(new CountryCode("PT"), scope.Country);
    }

    [Fact]
    public void Country_in_multiple_observed_countries_is_a_match()
    {
        var scope = new SourceExecutionScope.PublicationCountry(new CountryCode("PT"));
        var claim = new ScopedSourceGeographyClaim(
            new ObservedPublicationGeography.Countries(
                [new CountryCode("ES"), new CountryCode("PT")]));
        Assert.Equal(ScopedGeographyClaimAssessment.MatchesRequestedCountry,
            ScopedGeographyClaimEvaluator.Evaluate(scope, claim));
    }

    [Fact]
    public void Missing_or_unknown_claim_remains_unknown()
    {
        var scope = new SourceExecutionScope.PublicationCountry(new CountryCode("PT"));
        Assert.Equal(ScopedGeographyClaimAssessment.Unknown,
            ScopedGeographyClaimEvaluator.Evaluate(scope, null));
        Assert.Equal(ScopedGeographyClaimAssessment.Unknown,
            ScopedGeographyClaimEvaluator.Evaluate(scope,
                new ScopedSourceGeographyClaim(new ObservedPublicationGeography.Unknown())));
    }

    [Fact]
    public void Legacy_market_is_not_treated_as_publication_country()
    {
        var scope = new SourceExecutionScope.LegacyMarket(new CountryCode("PT"));
        var claim = new ScopedSourceGeographyClaim(
            new ObservedPublicationGeography.Countries([new CountryCode("ES")]));
        Assert.Equal(ScopedGeographyClaimAssessment.NotComparable,
            ScopedGeographyClaimEvaluator.Evaluate(scope, claim));
    }

    [Fact]
    public void Unconstrained_request_is_not_comparable_to_country_claim()
    {
        var scope = new SourceExecutionScope.UnconstrainedPublication();
        var claim = new ScopedSourceGeographyClaim(
            new ObservedPublicationGeography.Countries([new CountryCode("PT")]));
        Assert.Equal(ScopedGeographyClaimAssessment.NotComparable,
            ScopedGeographyClaimEvaluator.Evaluate(scope, claim));
    }
}
