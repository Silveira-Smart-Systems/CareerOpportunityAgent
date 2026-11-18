using COA.Application.Profile;
using COA.Domain.Profile;
using Xunit;

namespace COA.UnitTests.Profile;

public sealed class ProfileUseCasesTests
{
    [Fact]
    public async Task Create_uses_the_trusted_owner_and_returns_created_profile()
    {
        var store = new FakeProfileStore();
        var owner = Owner("https://issuer.example", "subject-a");
        var result = await new ProfileUseCases(store).CreateAsync(owner, CreateRequest());

        var created = Assert.IsType<CreateProfileResult.Created>(result);
        Assert.Same(owner, store.LastOwner);
        Assert.Equal(1, created.Profile.Version);
        Assert.Equal("Engineer", created.Profile.Headline);
    }

    [Fact]
    public async Task Create_returns_conflict_from_owner_scoped_store()
    {
        var store = new FakeProfileStore { CreateResult = new CreateProfileResult.Conflict() };

        var result = await new ProfileUseCases(store).CreateAsync(Owner(), CreateRequest());

        Assert.IsType<CreateProfileResult.Conflict>(result);
    }

    [Fact]
    public async Task Get_current_maps_missing_profile_to_not_found()
    {
        var store = new FakeProfileStore();

        var result = await new ProfileUseCases(store).GetCurrentAsync(Owner());

        Assert.IsType<ProfileReadResult.NotFound>(result);
    }

    [Fact]
    public async Task Get_version_returns_requested_historical_snapshot_for_trusted_owner()
    {
        var original = Profile();
        var current = original.Revise("Architect", new[] { Evidence("Azure") }, Preferences());
        var store = new FakeProfileStore { CurrentResult = current, VersionResult = original };
        var owner = Owner();

        var result = await new ProfileUseCases(store).GetVersionAsync(owner, 1);

        var found = Assert.IsType<ProfileReadResult.Found>(result);
        Assert.Same(owner, store.LastOwner);
        Assert.Equal(1, store.LastVersion);
        Assert.Same(original, found.Profile);
        Assert.Equal(1, found.Profile.Version);
        Assert.Equal("Engineer", found.Profile.Headline);
    }

    [Fact]
    public async Task Revision_with_stale_expected_version_returns_conflict_without_writing()
    {
        var store = new FakeProfileStore { CurrentResult = Profile() };

        var result = await new ProfileUseCases(store).ReviseAsync(Owner(), ReviseRequest(expectedVersion: 2));

        Assert.IsType<ReviseProfileResult.Conflict>(result);
        Assert.Equal(0, store.ReviseCalls);
    }

    [Fact]
    public async Task Revision_of_missing_profile_returns_not_found()
    {
        var store = new FakeProfileStore();

        var result = await new ProfileUseCases(store).ReviseAsync(Owner(), ReviseRequest(expectedVersion: 1));

        Assert.IsType<ReviseProfileResult.NotFound>(result);
        Assert.Equal(0, store.ReviseCalls);
    }

    [Fact]
    public async Task Revision_returns_store_conflict_when_profile_changes_concurrently()
    {
        var store = new FakeProfileStore
        {
            CurrentResult = Profile(),
            ReviseResult = new ReviseProfileResult.Conflict()
        };

        var result = await new ProfileUseCases(store).ReviseAsync(Owner(), ReviseRequest(expectedVersion: 1));

        Assert.IsType<ReviseProfileResult.Conflict>(result);
        Assert.Equal(1, store.ReviseCalls);
    }

    [Fact]
    public async Task Revision_returns_new_snapshot_and_forwards_owner_and_expected_version()
    {
        var original = Profile();
        var store = new FakeProfileStore { CurrentResult = original };
        var owner = Owner();

        var result = await new ProfileUseCases(store).ReviseAsync(owner, ReviseRequest(expectedVersion: 1));

        var revised = Assert.IsType<ReviseProfileResult.Revised>(result).Profile;
        Assert.Same(owner, store.LastOwner);
        Assert.Equal(1, store.LastExpectedVersion);
        Assert.Same(revised, store.LastRevisedProfile);
        Assert.Equal(2, revised.Version);
        Assert.Equal("Architect", revised.Headline);
        Assert.Equal(1, original.Version);
        Assert.Equal("Engineer", original.Headline);
        Assert.Equal("C#", original.Evidence[0].Claim);
    }

    [Fact]
    public async Task Revision_prioritizes_missing_profile_over_invalid_proposed_content()
    {
        var store = new FakeProfileStore();

        var result = await new ProfileUseCases(store).ReviseAsync(Owner(), InvalidReviseRequest());

        Assert.IsType<ReviseProfileResult.NotFound>(result);
        Assert.Equal(0, store.ReviseCalls);
    }

    [Fact]
    public async Task Revision_prioritizes_stale_version_over_invalid_proposed_content()
    {
        var store = new FakeProfileStore { CurrentResult = Profile() };

        var result = await new ProfileUseCases(store).ReviseAsync(Owner(), InvalidReviseRequest(expectedVersion: 2));

        Assert.IsType<ReviseProfileResult.Conflict>(result);
        Assert.Equal(0, store.ReviseCalls);
    }

    [Fact]
    public void Owner_identity_rejects_missing_or_oversized_claim_values_without_normalizing()
    {
        Assert.Throws<ArgumentException>(() => new OwnerIdentity("", "subject"));
        Assert.Throws<ArgumentException>(() => new OwnerIdentity("issuer", new string('x', OwnerIdentity.MaximumIdentifierLength + 1)));

        var owner = Owner("https://Issuer.example", "Subject-A");
        Assert.Equal("https://Issuer.example", owner.Issuer);
        Assert.Equal("Subject-A", owner.Subject);
        Assert.NotEqual(owner, Owner("https://issuer.example", "Subject-A"));
        Assert.NotEqual(owner, Owner("https://Issuer.example", "subject-a"));
        Assert.Equal(owner, Owner("https://Issuer.example", "Subject-A"));
        Assert.DoesNotContain(owner.Subject, owner.ToString());
    }

    [Fact]
    public async Task Revision_requires_a_positive_expected_version()
    {
        var store = new FakeProfileStore { CurrentResult = Profile() };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ProfileUseCases(store).ReviseAsync(Owner(), ReviseRequest(expectedVersion: 0)));

        Assert.Equal(0, store.ReviseCalls);
    }

    private static OwnerIdentity Owner(string issuer = "https://issuer.example", string subject = "subject-a") =>
        new(issuer, subject);

    private static CreateProfileRequest CreateRequest() =>
        new("Engineer", new[] { Evidence() }, Preferences());

    private static ReviseProfileRequest ReviseRequest(int expectedVersion) =>
        new(expectedVersion, "Architect", new[] { Evidence() }, Preferences());

    private static ReviseProfileRequest InvalidReviseRequest(int expectedVersion = 1) =>
        new(expectedVersion, "", Array.Empty<ProfileEvidence>(), Preferences());

    private static CareerProfile Profile() =>
        CareerProfile.Create(Guid.NewGuid(), "Engineer", new[] { Evidence() }, Preferences());

    private static ProfileEvidence Evidence(string claim = "C#") =>
        new(claim, "synthetic-source", EvidenceOrigin.UserProvided);

    private static CareerPreferences Preferences() =>
        new(WorkArrangement.Remote, true, 65m, "EUR", 0);

    private sealed class FakeProfileStore : IProfileStore
    {
        public CreateProfileResult? CreateResult { get; set; }

        public CareerProfile? CurrentResult { get; set; }

        public CareerProfile? VersionResult { get; set; }

        public ReviseProfileResult? ReviseResult { get; set; }

        public OwnerIdentity? LastOwner { get; private set; }

        public int? LastVersion { get; private set; }

        public int? LastExpectedVersion { get; private set; }

        public CareerProfile? LastRevisedProfile { get; private set; }

        public int ReviseCalls { get; private set; }

        public Task<CreateProfileResult> CreateAsync(OwnerIdentity owner, CareerProfile profile, CancellationToken cancellationToken = default)
        {
            LastOwner = owner;
            return Task.FromResult(CreateResult ?? new CreateProfileResult.Created(profile));
        }

        public Task<CareerProfile?> GetCurrentAsync(OwnerIdentity owner, CancellationToken cancellationToken = default)
        {
            LastOwner = owner;
            return Task.FromResult(CurrentResult);
        }

        public Task<CareerProfile?> GetVersionAsync(OwnerIdentity owner, int version, CancellationToken cancellationToken = default)
        {
            LastOwner = owner;
            LastVersion = version;
            return Task.FromResult(VersionResult);
        }

        public Task<ReviseProfileResult> ReviseAsync(
            OwnerIdentity owner,
            int expectedVersion,
            CareerProfile revisedProfile,
            CancellationToken cancellationToken = default)
        {
            LastOwner = owner;
            LastExpectedVersion = expectedVersion;
            LastRevisedProfile = revisedProfile;
            ReviseCalls++;
            return Task.FromResult(ReviseResult ?? new ReviseProfileResult.Revised(revisedProfile));
        }
    }
}
