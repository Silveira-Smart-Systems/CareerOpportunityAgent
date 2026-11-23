using COA.Domain.Matching;
using COA.Web.Api;
using COA.Web.Components;
using Xunit;

namespace COA.Web.Tests;

public sealed class OpportunityPresentationTests
{
    [Theory]
    [InlineData(EvidenceState.Matched, "Matched", "matched")]
    [InlineData(EvidenceState.Missing, "Missing", "missing")]
    [InlineData(EvidenceState.UnknownNotProven, "Unknown / not proven", "unknown")]
    public void Evidence_states_have_distinct_honest_labels(EvidenceState evidence, string label, string cssClass)
    {
        var presentation = OpportunityPresentation.Describe(evidence);

        Assert.Equal(label, presentation.Label);
        Assert.Equal(cssClass, presentation.CssClass);
    }

    [Fact]
    public void Ineligible_assessment_is_described_separately_from_score()
    {
        Assert.Equal("Not eligible", OpportunityPresentation.EligibilityLabel(false));
        Assert.Contains("required criterion", OpportunityPresentation.EligibilityDescription(false));
    }

    [Fact]
    public void Forbidden_opportunity_state_does_not_disclose_opportunity_details()
    {
        var message = OpportunityPresentation.DescribeLoadFailure(new CoaApiAuthorizationException());

        Assert.Equal("You are not authorized to access this opportunity. Its details are not shown.", message);
    }
}
