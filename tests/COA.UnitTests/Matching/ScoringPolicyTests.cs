using COA.Domain.Matching;
using Xunit;

namespace COA.UnitTests.Matching;

public sealed class ScoringPolicyTests
{
    [Fact]
    public void Initial_policy_is_versioned_and_weights_total_100()
    {
        var policy = ScoringPolicy.Initial;
        policy.Validate();
        Assert.Equal("mvp-0.1", policy.Version);
        Assert.Equal(100, policy.Weights.Values.Sum());
    }

    [Fact]
    public void Missing_dimensions_remain_unknown_and_do_not_gain_points()
    {
        var result = ScoringPolicy.Initial.Evaluate(
            new Dictionary<string, EvidenceState> { ["technical"] = EvidenceState.Matched },
            Array.Empty<string>());
        Assert.Equal(40m, result.Score);
        Assert.Equal(4, result.Criteria.Count(x => x.Evidence == EvidenceState.UnknownNotProven));
    }

    [Fact]
    public void Remote_hard_constraint_blocks_even_with_high_score()
    {
        var result = ScoringPolicy.Initial.Evaluate(
            new Dictionary<string, EvidenceState>
            {
                ["technical"] = EvidenceState.Matched,
                ["commercial"] = EvidenceState.Matched,
                ["work-model"] = EvidenceState.UnknownNotProven,
                ["seniority"] = EvidenceState.Matched,
                ["career-alignment"] = EvidenceState.Matched
            },
            new[] { "work-model" });
        Assert.Equal(85m, result.Score);
        Assert.False(result.Eligible);
    }

    [Fact]
    public void Unknown_dimensions_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ScoringPolicy.Initial.Evaluate(
            new Dictionary<string, EvidenceState> { ["other"] = EvidenceState.Matched },
            Array.Empty<string>()));
    }

    [Fact]
    public void Invalid_policy_weights_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ScoringPolicy(
            "invalid",
            new Dictionary<string, int> { ["technical"] = 40 }).Validate());
    }
}
