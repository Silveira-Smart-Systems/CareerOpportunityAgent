namespace COA.Application.Discovery;

/// <summary>
/// Diagnostic comparison of provider-reported geography against the requested
/// execution scope. A mismatch is a claim conflict, not proof of actual location.
/// LegacyMarket is intentionally not interpreted as publication geography.
/// </summary>
public static class ScopedGeographyClaimEvaluator
{
    public static ScopedGeographyClaimAssessment Evaluate(
        SourceExecutionScope requestedScope, ScopedSourceGeographyClaim? claim)
    {
        ArgumentNullException.ThrowIfNull(requestedScope);
        if (claim?.ObservedGeography is null ||
            claim.ObservedGeography is ObservedPublicationGeography.Unknown)
            return ScopedGeographyClaimAssessment.Unknown;

        if (claim.ObservedGeography is not ObservedPublicationGeography.Countries countries ||
            countries.CountryCodes.Count == 0)
            return ScopedGeographyClaimAssessment.Unknown;

        if (requestedScope is not SourceExecutionScope.PublicationCountry publication)
            return ScopedGeographyClaimAssessment.NotComparable;

        return countries.CountryCodes.Contains(publication.Country)
            ? ScopedGeographyClaimAssessment.MatchesRequestedCountry
            : ScopedGeographyClaimAssessment.ConflictsWithRequestedCountry;
    }
}

public enum ScopedGeographyClaimAssessment
{
    Unknown,
    NotComparable,
    MatchesRequestedCountry,
    ConflictsWithRequestedCountry
}
