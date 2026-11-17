namespace COA.Domain.Matching;

/// <summary>
/// A versioned set of scoring weights. The initial weights are hypotheses,
/// not validated ranking performance.
/// </summary>
public sealed record ScoringPolicy(
    string Version,
    IReadOnlyDictionary<string, int> Weights)
{
    public static ScoringPolicy Initial { get; } = new(
        "mvp-0.1",
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["technical"] = 40,
            ["commercial"] = 25,
            ["work-model"] = 15,
            ["seniority"] = 10,
            ["career-alignment"] = 10
        });

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version))
            throw new ArgumentException("Policy version is required.", nameof(Version));
        if (Weights is null || Weights.Count == 0)
            throw new ArgumentException("At least one weight is required.", nameof(Weights));
        if (Weights.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 0))
            throw new ArgumentException("Weights require nonempty codes and nonnegative values.", nameof(Weights));
        if (Weights.Sum(x => (long)x.Value) != 100)
            throw new ArgumentException("Weights must sum to 100.", nameof(Weights));
    }

    public MatchAssessment Evaluate(IReadOnlyDictionary<string, EvidenceState> evidence,
        IReadOnlyCollection<string> hardConstraints)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(hardConstraints);

        var weightKeys = new HashSet<string>(Weights.Keys, StringComparer.OrdinalIgnoreCase);
        if (evidence.Keys.Any(key => !weightKeys.Contains(key)))
            throw new ArgumentException("Evidence contains unknown dimensions.", nameof(evidence));

        var hard = new HashSet<string>(hardConstraints, StringComparer.OrdinalIgnoreCase);
        if (hard.Any(key => !weightKeys.Contains(key)))
            throw new ArgumentException("Hard constraint refers to unknown dimension.", nameof(hardConstraints));

        var criteria = Weights.Select(x => new MatchCriterion(
            x.Key,
            evidence.TryGetValue(x.Key, out var state) ? state : EvidenceState.UnknownNotProven,
            x.Value,
            hard.Contains(x.Key))).ToArray();

        return MatchEvaluator.Evaluate(criteria);
    }
}
