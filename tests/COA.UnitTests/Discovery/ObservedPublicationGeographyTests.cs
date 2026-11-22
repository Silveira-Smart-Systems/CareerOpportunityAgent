using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ObservedPublicationGeographyTests
{
    [Fact]
    public void Unknown_observation_does_not_change_requested_scope()
    {
        var requested = new SourceExecutionScope.UnconstrainedPublication();
        var provenance = new ScopedSourceProvenance(
            new SourceId("example"), "job-1", new Uri("https://jobs.example.org/1"),
            requested, DateTimeOffset.UtcNow)
        {
            GeographyClaim = new ScopedSourceGeographyClaim(new ObservedPublicationGeography.Unknown())
        };
        Assert.IsType<SourceExecutionScope.UnconstrainedPublication>(provenance.RequestedScope);
        Assert.IsType<ObservedPublicationGeography.Unknown>(provenance.GeographyClaim!.ObservedGeography);
    }

    [Fact]
    public void Provider_claim_does_not_rewrite_requested_publication_country()
    {
        var requested = new SourceExecutionScope.PublicationCountry(new CountryCode("PT"));
        var observed = new ObservedPublicationGeography.Countries([new CountryCode("ES")]);
        var provenance = new ScopedSourceProvenance(
            new SourceId("example"), "job-1", new Uri("https://jobs.example.org/1"),
            requested, DateTimeOffset.UtcNow)
        {
            GeographyClaim = new ScopedSourceGeographyClaim(observed, "publication_country")
        };
        Assert.Equal(requested, provenance.RequestedScope);
        Assert.Equal(new CountryCode("ES"),
            Assert.Single(Assert.IsType<ObservedPublicationGeography.Countries>(
                provenance.GeographyClaim!.ObservedGeography).CountryCodes));
    }

    [Fact]
    public void Observed_countries_must_be_nonempty_and_unique()
    {
        Assert.Throws<ArgumentException>(() => new ObservedPublicationGeography.Countries([]));
        Assert.Throws<ArgumentException>(() => new ObservedPublicationGeography.Countries(
            [new CountryCode("PT"), new CountryCode("PT")]));
    }

    [Fact]
    public void Existing_scoped_provenance_constructor_remains_compatible()
    {
        var provenance = new ScopedSourceProvenance(
            new SourceId("example"), "job-1", new Uri("https://jobs.example.org/1"),
            new SourceExecutionScope.LegacyMarket(new CountryCode("PT")), DateTimeOffset.UtcNow);
        Assert.Null(provenance.GeographyClaim);
    }
}
