using COA.Domain.Matching;

namespace COA.Web.Components;

public sealed record EvidencePresentation(string Label, string Description, string CssClass);
public sealed record RequestFailurePresentation(string Heading, string Message, bool CanRetry, bool RequiresSignIn = false);

public static class OpportunityPresentation
{
    public static EvidencePresentation Describe(EvidenceState evidence) => evidence switch
    {
        EvidenceState.Matched => new("Matched", "Evidence supports this criterion.", "matched"),
        EvidenceState.Missing => new("Missing", "Evidence indicates this criterion is not met.", "missing"),
        _ => new("Unknown / not proven", "There is not enough evidence to confirm this criterion.", "unknown")
    };

    public static string EligibilityLabel(bool eligible) => eligible ? "Eligible" : "Not eligible";

    public static string EligibilityDescription(bool eligible) => eligible
        ? "No required criterion lacks matching evidence. Eligibility can be true even with a 0% score."
        : "At least one required criterion does not have matching evidence.";

    public static RequestFailurePresentation DescribeRequestFailure(Exception exception) => exception switch
    {
        Api.CoaApiAuthenticationException => new(
            "Sign in required",
            "Your session needs a new sign-in before it can access the COA API.",
            CanRetry: false,
            RequiresSignIn: true),
        Api.CoaApiAuthorizationException => new(
            "Access denied",
            "You are not authorized to access this opportunity. Its details are not shown.",
            CanRetry: false),
        Api.CoaApiConflictException => new(
            "Opportunity already imported",
            "This opportunity has already been imported.",
            CanRetry: false),
        Api.CoaApiRequestException => new(
            "Opportunity unavailable",
            "The COA API could not complete the request. Please try again.",
            CanRetry: true),
        _ => new(
            "Opportunity unavailable",
            "The opportunity request could not be completed. Please try again.",
            CanRetry: true)
    };

    public static RequestFailurePresentation DescribeNotFound() => new(
        "Opportunity not found",
        "This opportunity is unavailable or no longer belongs to your workspace.",
        CanRetry: false);
}
