using COA.Application.Discovery;
using Xunit;

namespace COA.UnitTests.Discovery;

public sealed class ScopedSourceProvenanceValidatorTests
{
    private static readonly SourceId Source = new("example");
    private static readonly SourceExecutionScope Scope =
        new SourceExecutionScope.UnconstrainedPublication();
    private static readonly ScopedSourceExecutionTarget Target = new(Source, Scope);

    [Fact]
    public void Valid_https_provenance_preserves_unconstrained_scope()
    {
        var provenance = NewProvenance(new Uri("https://jobs.example.org/123"));
        Assert.True(ScopedSourceProvenanceValidator.IsValid(Target, provenance));
    }

    [Theory]
    [InlineData("file:///tmp/job")]
    [InlineData("ftp://jobs.example.org/123")]
    [InlineData("https://user:secret@jobs.example.org/123")]
    public void Rejects_non_web_or_credential_bearing_urls(string url)
    {
        Assert.False(ScopedSourceProvenanceValidator.IsValid(Target,
            NewProvenance(new Uri(url))));
    }

    [Fact]
    public void Rejects_wrong_requested_scope_without_converting_to_legacy_market()
    {
        var other = new ScopedSourceExecutionTarget(Source,
            new SourceExecutionScope.PublicationCountry(new CountryCode("PT")));
        Assert.False(ScopedSourceProvenanceValidator.IsValid(other,
            NewProvenance(new Uri("https://jobs.example.org/123"))));
    }

    [Fact]
    public void Rejects_missing_external_id()
    {
        var original = NewProvenance(new Uri("https://jobs.example.org/123"));
        Assert.False(ScopedSourceProvenanceValidator.IsValid(Target,
            original with { ExternalId = " " }));
    }

    [Fact]
    public void Rejects_missing_retrieval_timestamp()
    {
        var original = NewProvenance(new Uri("https://jobs.example.org/123"));
        Assert.False(ScopedSourceProvenanceValidator.IsValid(Target,
            original with { RetrievedAtUtc = default }));
    }

    private static ScopedSourceProvenance NewProvenance(Uri url) =>
        new(Source, "job-123", url, Scope, DateTimeOffset.UtcNow);
}
