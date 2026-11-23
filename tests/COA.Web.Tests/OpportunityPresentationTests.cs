using COA.Domain.Matching;
using COA.Web.Api;
using COA.Web.Components;
using System.Net;
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
    public void Eligibility_is_described_separately_from_score()
    {
        Assert.Equal("Not eligible", OpportunityPresentation.EligibilityLabel(false));
        Assert.Contains("required criterion", OpportunityPresentation.EligibilityDescription(false));
        Assert.Contains("0% score", OpportunityPresentation.EligibilityDescription(true));
    }

    [Fact]
    public void Authentication_failure_requires_sign_in_without_retry()
    {
        var failure = OpportunityPresentation.DescribeRequestFailure(
            new CoaApiAuthenticationException("expired"));

        Assert.Equal("Sign in required", failure.Heading);
        Assert.True(failure.RequiresSignIn);
        Assert.False(failure.CanRetry);
    }

    [Fact]
    public void Forbidden_opportunity_state_does_not_disclose_details_or_offer_retry()
    {
        var failure = OpportunityPresentation.DescribeRequestFailure(new CoaApiAuthorizationException());

        Assert.Equal("Access denied", failure.Heading);
        Assert.Equal("You are not authorized to access this opportunity. Its details are not shown.", failure.Message);
        Assert.False(failure.CanRetry);
    }

    [Fact]
    public void Not_found_state_does_not_offer_retry()
    {
        var failure = OpportunityPresentation.DescribeNotFound();

        Assert.Equal("Opportunity not found", failure.Heading);
        Assert.False(failure.CanRetry);
    }

    [Fact]
    public void Server_failure_is_retryable_without_exposing_response_details()
    {
        var failure = OpportunityPresentation.DescribeRequestFailure(
            new CoaApiRequestException(HttpStatusCode.InternalServerError, "internal details"));

        Assert.Equal("Opportunity unavailable", failure.Heading);
        Assert.True(failure.CanRetry);
        Assert.DoesNotContain("internal details", failure.Message);
    }
}
