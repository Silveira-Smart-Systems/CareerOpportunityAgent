namespace COA.Domain.Matching;

/// <summary>Evidence states must not turn unknown facts into proven matches.</summary>
public enum EvidenceState { Matched, Missing, UnknownNotProven }

public sealed record MatchCriterion(
    string Code,
    EvidenceState Evidence,
    int Weight,
    bool IsHardConstraint = false);

public sealed record CriterionAssessment(
    string Code,
    EvidenceState Evidence,
    int Weight,
    bool IsHardConstraint);

public sealed record MatchAssessment(
    decimal Score,
    bool Eligible,
    IReadOnlyList<CriterionAssessment> Criteria);

public static class MatchEvaluator
{
    public static MatchAssessment Evaluate(IReadOnlyCollection<MatchCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            throw new ArgumentException("At least one criterion is required.", nameof(criteria));
        if (criteria.Any(x => string.IsNullOrWhiteSpace(x.Code) || x.Weight < 0))
            throw new ArgumentException("Criteria must have a code and nonnegative weight.", nameof(criteria));
        if (criteria.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != criteria.Count)
            throw new ArgumentException("Criterion codes must be unique.", nameof(criteria));

        var totalWeight = criteria.Sum(x => (long)x.Weight);
        if (totalWeight <= 0)
            throw new ArgumentException("Total weight must be positive.", nameof(criteria));

        var matchedWeight = criteria.Where(x => x.Evidence == EvidenceState.Matched).Sum(x => (long)x.Weight);
        var score = Math.Round(100m * matchedWeight / totalWeight, 2, MidpointRounding.AwayFromZero);
        var eligible = !criteria.Any(x => x.IsHardConstraint && x.Evidence != EvidenceState.Matched);

        return new MatchAssessment(
            score,
            eligible,
            criteria.Select(x => new CriterionAssessment(x.Code, x.Evidence, x.Weight, x.IsHardConstraint)).ToArray());
    }
}
