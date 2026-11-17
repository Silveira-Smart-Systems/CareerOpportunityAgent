using COA.Domain.Matching;

namespace COA.Application.Matching;

public sealed record EvaluateMatchRequest(IReadOnlyList<MatchCriterion> Criteria);

public static class EvaluateMatch
{
    public static MatchAssessment Execute(EvaluateMatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MatchEvaluator.Evaluate(request.Criteria);
    }
}
