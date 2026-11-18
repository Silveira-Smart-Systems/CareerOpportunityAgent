using System.Text.Json.Serialization;
using COA.Application.Profile;
using COA.Domain.Profile;

namespace COA.Api.Profile;

public record CreateProfileHttpRequest
{
    public required string Headline { get; init; }
    public required IReadOnlyList<ProfileEvidenceHttpRequest> Evidence { get; init; }
    public required CareerPreferencesHttpRequest Preferences { get; init; }

    public CreateProfileRequest ToApplication() => new(
        Headline,
        Evidence.Select(item => item.ToDomain()).ToArray(),
        Preferences.ToDomain());
}

public sealed record ReviseProfileHttpRequest : CreateProfileHttpRequest
{
    public required int ExpectedVersion { get; init; }

    public ReviseProfileRequest ToReviseApplication() => new(
        ExpectedVersion,
        Headline,
        Evidence.Select(item => item.ToDomain()).ToArray(),
        Preferences.ToDomain());
}

public sealed record ProfileEvidenceHttpRequest
{
    public required string Claim { get; init; }
    public required string SourceReference { get; init; }
    public required EvidenceOrigin Origin { get; init; }

    public ProfileEvidence ToDomain() => new(Claim, SourceReference, Origin);
}

public sealed record CareerPreferencesHttpRequest
{
    public required WorkArrangement PreferredArrangement { get; init; }
    public required bool RemoteOnly { get; init; }
    public decimal? MinimumHourlyRate { get; init; }
    public string? Currency { get; init; }
    public int? MaximumOfficeDaysPerWeek { get; init; }

    public CareerPreferences ToDomain() => new(
        PreferredArrangement,
        RemoteOnly,
        MinimumHourlyRate,
        Currency,
        MaximumOfficeDaysPerWeek);
}

public sealed record ProfileHttpResponse(
    Guid Id,
    int Version,
    string Headline,
    IReadOnlyList<ProfileEvidenceHttpResponse> Evidence,
    CareerPreferencesHttpResponse Preferences)
{
    public static ProfileHttpResponse FromDomain(CareerProfile profile) => new(
        profile.Id,
        profile.Version,
        profile.Headline,
        profile.Evidence.Select(item => new ProfileEvidenceHttpResponse(
            item.Claim,
            item.SourceReference,
            item.Origin)).ToArray(),
        new CareerPreferencesHttpResponse(
            profile.Preferences.PreferredArrangement,
            profile.Preferences.RemoteOnly,
            profile.Preferences.MinimumHourlyRate,
            profile.Preferences.Currency,
            profile.Preferences.MaximumOfficeDaysPerWeek));
}

public sealed record ProfileEvidenceHttpResponse(
    string Claim,
    string SourceReference,
    EvidenceOrigin Origin);

public sealed record CareerPreferencesHttpResponse(
    WorkArrangement PreferredArrangement,
    bool RemoteOnly,
    decimal? MinimumHourlyRate,
    string? Currency,
    int? MaximumOfficeDaysPerWeek);
