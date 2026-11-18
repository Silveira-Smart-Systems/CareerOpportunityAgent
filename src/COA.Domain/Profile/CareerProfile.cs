namespace COA.Domain.Profile;

public enum EvidenceOrigin
{
    UserProvided,
    CvImport,
    VerifiedCorrection
}

public sealed record ProfileEvidence(
    string Claim,
    string SourceReference,
    EvidenceOrigin Origin)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Claim) || Claim.Length > 500)
            throw new ArgumentException("Claim is required and limited to 500 characters.");
        if (string.IsNullOrWhiteSpace(SourceReference) || SourceReference.Length > 300)
            throw new ArgumentException("Source reference is required and limited to 300 characters.");
        if (!Enum.IsDefined(Origin))
            throw new ArgumentException("Evidence origin is invalid.");
    }
}

public enum WorkArrangement { Remote, Hybrid, OnSite }

public sealed record CareerPreferences(
    WorkArrangement PreferredArrangement,
    bool RemoteOnly,
    decimal? MinimumHourlyRate,
    string? Currency,
    int? MaximumOfficeDaysPerWeek)
{
    public void Validate()
    {
        if (!Enum.IsDefined(PreferredArrangement))
            throw new ArgumentException("Work arrangement is invalid.");
        if (RemoteOnly && PreferredArrangement != WorkArrangement.Remote)
            throw new ArgumentException("RemoteOnly requires Remote preferred arrangement.");
        if (MinimumHourlyRate is < 0 or > 100000)
            throw new ArgumentException("Minimum hourly rate is outside supported range.");
        if (MinimumHourlyRate.HasValue && MinimumHourlyRate.Value % 0.01m != 0)
            throw new ArgumentException("Minimum hourly rate supports at most two decimal places.");
        if (MinimumHourlyRate.HasValue && (Currency is null || Currency.Length != 3 ||
            !Currency.All(char.IsLetter)))
            throw new ArgumentException("Three-letter currency is required with minimum rate.");
        if (MaximumOfficeDaysPerWeek is < 0 or > 5)
            throw new ArgumentException("Office days must be between 0 and 5.");
        if (RemoteOnly && MaximumOfficeDaysPerWeek is > 0)
            throw new ArgumentException("RemoteOnly is incompatible with office days.");
    }
}

public sealed record CareerProfile(
    Guid Id,
    int Version,
    string Headline,
    IReadOnlyList<ProfileEvidence> Evidence,
    CareerPreferences Preferences)
{
    public static CareerProfile Create(
        Guid id,
        string headline,
        IReadOnlyList<ProfileEvidence> evidence,
        CareerPreferences preferences)
    {
        var profile = new CareerProfile(id, 1, headline, evidence.ToArray(), preferences);
        profile.Validate();
        return profile;
    }

    public CareerProfile Revise(
        string headline,
        IReadOnlyList<ProfileEvidence> evidence,
        CareerPreferences preferences)
    {
        var revised = new CareerProfile(Id, checked(Version + 1), headline, evidence.ToArray(), preferences);
        revised.Validate();
        return revised;
    }

    public void Validate()
    {
        if (Id == Guid.Empty)
            throw new ArgumentException("Profile identifier is required.");
        if (Version < 1)
            throw new ArgumentException("Profile version must be positive.");
        if (string.IsNullOrWhiteSpace(Headline) || Headline.Length > 200)
            throw new ArgumentException("Headline is required and limited to 200 characters.");
        ArgumentNullException.ThrowIfNull(Evidence);
        ArgumentNullException.ThrowIfNull(Preferences);
        if (Evidence.Count > 200)
            throw new ArgumentException("Too many evidence claims.");
        foreach (var claim in Evidence)
        {
            ArgumentNullException.ThrowIfNull(claim);
            claim.Validate();
        }
        Preferences.Validate();
    }
}
