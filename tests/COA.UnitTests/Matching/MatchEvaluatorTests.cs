using COA.Domain.Matching;
using Xunit;

namespace COA.UnitTests.Matching;

public sealed class MatchEvaluatorTests
{
    [Fact]
    public void Known_match_is_scored_deterministically()
    {
        var input = new[]
        {
            new MatchCriterion("technical", EvidenceState.Matched, 40),
            new MatchCriterion("commercial", EvidenceState.Missing, 25),
            new MatchCriterion("work-model", EvidenceState.Matched, 15),
            new MatchCriterion("seniority", EvidenceState.UnknownNotProven, 10),
            new MatchCriterion("career", EvidenceState.Matched, 10)
        };
        var first = MatchEvaluator.Evaluate(input);
        var second = MatchEvaluator.Evaluate(input);
        Assert.Equal(65m, first.Score);
        Assert.Equal(first, second);
        Assert.True(first.Eligible);
    }

    [Theory]
    [InlineData(EvidenceState.Missing)]
    [InlineData(EvidenceState.UnknownNotProven)]
    public void Hard_constraint_not_proven_blocks_eligibility(EvidenceState state)
    {
        var result = MatchEvaluator.Evaluate(new[]
        {
            new MatchCriterion("technical", EvidenceState.Matched, 90),
            new MatchCriterion("remote", state, 10, true)
        });
        Assert.Equal(90m, result.Score);
        Assert.False(result.Eligible);
    }

    [Fact]
    public void Unknown_is_not_counted_as_matched()
    {
        var result = MatchEvaluator.Evaluate(new[]
        {
            new MatchCriterion("salary", EvidenceState.UnknownNotProven, 25),
            new MatchCriterion("technical", EvidenceState.Matched, 75)
        });
        Assert.Equal(75m, result.Score);
    }

    [Fact]
    public void Duplicate_codes_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => MatchEvaluator.Evaluate(new[]
        {
            new MatchCriterion("remote", EvidenceState.Matched, 1),
            new MatchCriterion("REMOTE", EvidenceState.Matched, 1)
        }));
    }
}
