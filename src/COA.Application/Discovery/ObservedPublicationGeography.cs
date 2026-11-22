namespace COA.Application.Discovery;

/// <summary>
/// Provider-reported publication geography, independent of the scope requested
/// by the caller. Unknown is not equivalent to an unconstrained request.
/// This is an additive contract; no provider is activated by introducing it.
/// </summary>
public abstract record ObservedPublicationGeography
{
    private ObservedPublicationGeography() { }

    public sealed record Unknown : ObservedPublicationGeography;

    public sealed record Countries : ObservedPublicationGeography
    {
        public Countries(IEnumerable<CountryCode> countryCodes)
        {
            var values = (countryCodes ?? throw new ArgumentNullException(nameof(countryCodes))).ToArray();
            if (values.Length == 0 || values.Distinct().Count() != values.Length)
                throw new ArgumentException("Observed countries must be nonempty and unique.", nameof(countryCodes));
            CountryCodes = Array.AsReadOnly(values);
        }

        public IReadOnlyList<CountryCode> CountryCodes { get; }
    }
}

/// <summary>
/// Explicit source claim. The claim is not independent verification of geography,
/// licensing, source authorization, or evidence of rights to ingest content.
/// </summary>
public sealed record ScopedSourceGeographyClaim(
    ObservedPublicationGeography ObservedGeography,
    string? ProviderFieldName = null);
