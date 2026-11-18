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
    public async Task Get_version_returns_found_profile_for_trusted_owner()
    {
        var profile = Profile();
        var store = new FakeProfileStore { VersionResult = profile };
        var owner = Owner();

        var result = await new ProfileUseCases(store).GetVersionAsync(owner, 1);

        var found = Assert.IsType<ProfileReadResult.Found>(result);
        Assert.Same(owner, store.LastOwner);
        Assert.Same(profile, found.Profile);
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
    public void Owner_identity_rejects_missing_or_oversized_claim_values_without_normalizing()
    {
        Assert.Throws<ArgumentException>(() => new OwnerIdentity("", "subject"));
        Assert.Throws<ArgumentException>(() => new OwnerIdentity("issuer", new string('x', OwnerIdentity.MaximumIdentifierLength + 1)));

        var owner = Owner("https://Issuer.example", "Subject-A");
        Assert.Equal("https://Issuer.example", owner.Issuer);
        Assert.Equal("Subject-A", owner.Subject);
        Assert.NotEqual(owner, Owner("https://issuer.example", "Subject-A"));
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

    private static CareerProfile Profile() =>
        CareerProfile.Create(Guid.NewGuid(), "Engineer", new[] { Evidence() }, Preferences());

    private static ProfileEvidence Evidence() =>
        new("C#", "synthetic-source", EvidenceOrigin.UserProvided);

    private static CareerPreferences Preferences() =>
        new(WorkArrangement.Remote, true, 65m, "EUR", 0);

    private sealed class FakeProfileStore : IProfileStore
    {
        public CreateProfileResult? CreateResult { get; set; }

        public CareerProfile? CurrentResult { get; set; }

        public CareerProfile? VersionResult { get; set; }

        public ReviseProfileResult? ReviseResult { get; set; }

        public OwnerIdentity? LastOwner { get; private set; }

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
            return Task.FromResult(VersionResult);
        }

        public Task<ReviseProfileResult> ReviseAsync(
            OwnerIdentity owner,
            int expectedVersion,
            CareerProfile revisedProfile,
            CancellationToken cancellationToken = default)
        {
            LastOwner = owner;
            ReviseCalls++;
            return Task.FromResult(ReviseResult ?? new ReviseProfileResult.Revised(revisedProfile));
        }
    }
}
