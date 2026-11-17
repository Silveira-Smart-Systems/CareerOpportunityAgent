using COA.Domain.Profile;
using Xunit;

namespace COA.UnitTests.Profile;

public sealed class CareerProfileTests
{
    private static CareerPreferences RemotePreferences() =>
        new(WorkArrangement.Remote, true, 65m, "EUR", 0);

    [Fact]
    public void Creating_profile_starts_at_version_one_and_keeps_evidence_source()
    {
        var id = Guid.NewGuid();
        var evidence = new[] { new ProfileEvidence(".NET architecture", "synthetic-fixture-1", EvidenceOrigin.UserProvided) };
        var profile = CareerProfile.Create(id, "Software Architect", evidence, RemotePreferences());

        Assert.Equal(id, profile.Id);
        Assert.Equal(1, profile.Version);
        Assert.Equal("synthetic-fixture-1", profile.Evidence[0].SourceReference);
    }

    [Fact]
    public void Revision_increments_version_without_mutating_previous_snapshot()
    {
        var original = CareerProfile.Create(Guid.NewGuid(), "Engineer",
            new[] { new ProfileEvidence("C#", "fixture-1", EvidenceOrigin.UserProvided) },
            RemotePreferences());
        var revised = original.Revise("Architect",
            new[] { new ProfileEvidence("Azure", "fixture-2", EvidenceOrigin.VerifiedCorrection) },
            RemotePreferences());

        Assert.Equal(1, original.Version);
        Assert.Equal("C#", original.Evidence[0].Claim);
        Assert.Equal(2, revised.Version);
        Assert.Equal(original.Id, revised.Id);
        Assert.Equal("fixture-2", revised.Evidence[0].SourceReference);
    }

    [Fact]
    public void Remote_only_cannot_require_office_days()
    {
        Assert.Throws<ArgumentException>(() => CareerProfile.Create(
            Guid.NewGuid(), "Engineer", Array.Empty<ProfileEvidence>(),
            new CareerPreferences(WorkArrangement.Remote, true, null, null, 2)));
    }

    [Fact]
    public void Rate_requires_valid_currency()
    {
        Assert.Throws<ArgumentException>(() => CareerProfile.Create(
            Guid.NewGuid(), "Engineer", Array.Empty<ProfileEvidence>(),
            new CareerPreferences(WorkArrangement.Remote, true, 80, "EU", 0)));
    }

    [Fact]
    public void Evidence_must_have_provenance()
    {
        Assert.Throws<ArgumentException>(() => CareerProfile.Create(
            Guid.NewGuid(), "Engineer",
            new[] { new ProfileEvidence("Azure", "", EvidenceOrigin.CvImport) },
            RemotePreferences()));
    }

    [Fact]
    public void Invalid_work_arrangement_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CareerProfile.Create(
            Guid.NewGuid(), "Engineer", Array.Empty<ProfileEvidence>(),
            new CareerPreferences((WorkArrangement)999, false, null, null, null)));
    }
}
