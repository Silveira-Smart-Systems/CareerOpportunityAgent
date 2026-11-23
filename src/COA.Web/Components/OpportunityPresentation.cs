using COA.Domain.Matching;

namespace COA.Web.Components;

public sealed record EvidencePresentation(string Label, string Description, string CssClass);

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
        ? "All required criteria have matching evidence."
        : "At least one required criterion does not have matching evidence.";

    public static string DescribeLoadFailure(Exception exception) => exception switch
    {
        Api.CoaApiAuthenticationException => "Your session needs a new sign-in before it can access the COA API.",
        Api.CoaApiAuthorizationException => "You are not authorized to access this opportunity. Its details are not shown.",
        Api.CoaApiConflictException => "This opportunity has already been imported.",
        Api.CoaApiRequestException requestException => $"The COA API could not complete the request (HTTP {(int)requestException.StatusCode}).",
        _ => "The opportunity request could not be completed. Please try again."
    };
}
