namespace COA.Application.Discovery;

/// <summary>
/// Additive validation of scoped provenance. A requested scope is execution intent,
/// not proof of an observed publication geography or licensing rights.
/// </summary>
public static class ScopedSourceProvenanceValidator
{
    public static bool IsValid(ScopedSourceExecutionTarget expected, ScopedSourceProvenance? provenance)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return provenance is not null &&
            provenance.SourceId == expected.SourceId &&
            provenance.RequestedScope == expected.Scope &&
            !string.IsNullOrWhiteSpace(provenance.ExternalId) &&
            provenance.SourceUrl is { IsAbsoluteUri: true } url &&
            (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp) &&
            string.IsNullOrEmpty(url.UserInfo) &&
            !string.IsNullOrWhiteSpace(url.Host) &&
            provenance.RetrievedAtUtc != default &&
            provenance.RetrievedAtUtc.Offset == TimeSpan.Zero;
    }
}
