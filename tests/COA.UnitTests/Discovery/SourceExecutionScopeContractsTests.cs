using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class SourceExecutionScopeContractsTests
{
    private static readonly CountryCode Portugal = new("PT");

    [Fact]
    public void Legacy_market_publication_country_and_unconstrained_are_distinct()
    {
        SourceExecutionScope legacy = new SourceExecutionScope.LegacyMarket(Portugal);
        SourceExecutionScope publication = new SourceExecutionScope.PublicationCountry(Portugal);
        SourceExecutionScope unconstrained = new SourceExecutionScope.UnconstrainedPublication();

        Assert.NotEqual(legacy, publication);
        Assert.NotEqual(publication, unconstrained);
        Assert.NotEqual(legacy, unconstrained);
        Assert.Equal(new SourceExecutionScope.LegacyMarket(Portugal), legacy);
    }

    [Fact]
    public void Invalid_countries_and_missing_evidence_fail_closed()
    {
        Assert.Throws<ArgumentException>(() => new SourceExecutionScope.LegacyMarket(default));
        Assert.Throws<ArgumentException>(() => new SourceExecutionScope.PublicationCountry(default));
        Assert.Throws<ArgumentNullException>(() => new SourceScopeCapability(null!, "reference"));
        Assert.Throws<ArgumentException>(() => new SourceScopeCapability(new SourceExecutionScope.UnconstrainedPublication(), " "));
        Assert.Throws<ArgumentNullException>(() => new SourceScopeCapabilities(null!));
    }

    [Fact]
    public void Only_explicit_evidence_backed_scopes_are_supported()
    {
        var scope = new SourceExecutionScope.PublicationCountry(Portugal);
        var input = new List<SourceScopeCapability> { new(scope, " ADR-0014 ") };
        var capabilities = new SourceScopeCapabilities(input);
        input.Clear();

        Assert.True(capabilities.Supports(new SourceExecutionScope.PublicationCountry(Portugal)));
        Assert.False(capabilities.Supports(new SourceExecutionScope.LegacyMarket(Portugal)));
        Assert.False(capabilities.Supports(new SourceExecutionScope.UnconstrainedPublication()));
        Assert.False(capabilities.Supports(null!));
        Assert.Equal("ADR-0014", Assert.Single(capabilities.Scopes).EvidenceReference);
    }

    [Fact]
    public void Duplicate_and_null_capabilities_are_rejected()
    {
        var scope = new SourceExecutionScope.UnconstrainedPublication();
        Assert.Throws<ArgumentException>(() => new SourceScopeCapabilities([
            new SourceScopeCapability(scope, "first"),
            new SourceScopeCapability(new SourceExecutionScope.UnconstrainedPublication(), "second")
        ]));
        Assert.Throws<ArgumentException>(() => new SourceScopeCapabilities([null!]));
    }

    [Fact]
    public void Scoped_target_rejects_missing_identity()
    {
        Assert.Throws<ArgumentException>(() => new ScopedSourceExecutionTarget(default, new SourceExecutionScope.UnconstrainedPublication()));
        Assert.Throws<ArgumentNullException>(() => new ScopedSourceExecutionTarget(new SourceId("source"), null!));
        var target = new ScopedSourceExecutionTarget(new SourceId("source"), new SourceExecutionScope.UnconstrainedPublication());
        Assert.Equal(new SourceId("source"), target.SourceId);
    }
}
