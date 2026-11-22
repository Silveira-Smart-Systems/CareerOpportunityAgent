using System.Collections.Frozen;

namespace COA.Application.Discovery;

/// <summary>
/// Identifies the scope of one source execution without treating advertised geography,
/// publication market, and remote eligibility as interchangeable.
/// </summary>
public abstract record SourceExecutionScope
{
    private SourceExecutionScope() { }

    /// <summary>Compatibility scope for existing source-market executions.</summary>
    public sealed record LegacyMarket : SourceExecutionScope
    {
        public LegacyMarket(CountryCode market)
        {
            DiscoveryGeographyValidation.ValidateCountry(market, nameof(market));
            Market = market;
        }

        public CountryCode Market { get; }
    }

    /// <summary>A source query constrained to a known publication market.</summary>
    public sealed record PublicationCountry : SourceExecutionScope
    {
        public PublicationCountry(CountryCode country)
        {
            DiscoveryGeographyValidation.ValidateCountry(country, nameof(country));
            Country = country;
        }

        public CountryCode Country { get; }
    }

    /// <summary>A source query with no publication-market constraint.</summary>
    public sealed record UnconstrainedPublication : SourceExecutionScope;
}

/// <summary>
/// Declaration of a source's ability to execute a scope, accompanied by a reference
/// to supporting evidence. This contract validates only the presence of a reference:
/// it does NOT verify the evidence or authorize execution. Provider registration must
/// verify the evidence before declaring support. Absence of a declaration means
/// unsupported; it must never imply broad coverage.
/// </summary>
public sealed record SourceScopeCapability
{
    public SourceScopeCapability(SourceExecutionScope scope, string evidenceReference)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        EvidenceReference = string.IsNullOrWhiteSpace(evidenceReference)
            ? throw new ArgumentException("Evidence is required for an execution scope.", nameof(evidenceReference))
            : evidenceReference.Trim();
    }

    public SourceExecutionScope Scope { get; }
    public string EvidenceReference { get; }
}

/// <summary>
/// An immutable set of declared execution scopes. Supports() checks membership only;
/// it does not validate provider evidence, licensing, quota or authorization.
/// Provider-specific registration must verify evidence before supplying capabilities.
/// </summary>
public sealed class SourceScopeCapabilities
{
    private readonly IReadOnlyDictionary<SourceExecutionScope, SourceScopeCapability> _scopes;

    public SourceScopeCapabilities(IEnumerable<SourceScopeCapability> scopes)
    {
        var values = (scopes ?? throw new ArgumentNullException(nameof(scopes))).ToArray();
        if (values.Any(value => value is null))
            throw new ArgumentException("Null scope capability is not allowed.", nameof(scopes));
        if (values.Select(value => value.Scope).Distinct().Count() != values.Length)
            throw new ArgumentException("Duplicate execution scopes are not allowed.", nameof(scopes));

        _scopes = values.ToFrozenDictionary(value => value.Scope);
    }

    public IReadOnlyCollection<SourceScopeCapability> Scopes => _scopes.Values.ToArray();

    public bool Supports(SourceExecutionScope scope) =>
        scope is not null && _scopes.ContainsKey(scope);
}

/// <summary>
/// An execution identity. LegacyMarket and PublicationCountry are deliberately distinct;
/// callers must not silently convert one to the other or infer unconstrained coverage.
/// This type is intentionally not wired to the legacy gate until
/// Planner/Orchestrator/Gate changes and regression tests are reviewed together.
/// </summary>
public sealed record ScopedSourceExecutionTarget
{
    public ScopedSourceExecutionTarget(SourceId sourceId, SourceExecutionScope scope)
    {
        if (string.IsNullOrWhiteSpace(sourceId.Value))
            throw new ArgumentException("Source ID is required.", nameof(sourceId));
        SourceId = sourceId;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public SourceId SourceId { get; }
    public SourceExecutionScope Scope { get; }
}
