using COA.Domain.Profile;

namespace COA.Application.Profile;

public sealed record CreateProfileRequest(
    string Headline,
    IReadOnlyList<ProfileEvidence> Evidence,
    CareerPreferences Preferences);

public sealed record ReviseProfileRequest(
    int ExpectedVersion,
    string Headline,
    IReadOnlyList<ProfileEvidence> Evidence,
    CareerPreferences Preferences);

public sealed class ProfileUseCases(IProfileStore store)
{
    private readonly IProfileStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CreateProfileResult> CreateAsync(
        OwnerIdentity owner,
        CreateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);

        var profile = CareerProfile.Create(
            Guid.NewGuid(),
            request.Headline,
            request.Evidence ?? throw new ArgumentNullException(nameof(request.Evidence)),
            request.Preferences ?? throw new ArgumentNullException(nameof(request.Preferences)));

        return _store.CreateAsync(owner, profile, cancellationToken);
    }

    public async Task<ProfileReadResult> GetCurrentAsync(
        OwnerIdentity owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var profile = await _store.GetCurrentAsync(owner, cancellationToken);
        return profile is null
            ? new ProfileReadResult.NotFound()
            : new ProfileReadResult.Found(profile);
    }

    public async Task<ProfileReadResult> GetVersionAsync(
        OwnerIdentity owner,
        int version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be positive.");

        var profile = await _store.GetVersionAsync(owner, version, cancellationToken);
        return profile is null
            ? new ProfileReadResult.NotFound()
            : new ProfileReadResult.Found(profile);
    }

    public async Task<ReviseProfileResult> ReviseAsync(
        OwnerIdentity owner,
        ReviseProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(request.ExpectedVersion), "Expected version must be positive.");

        var current = await _store.GetCurrentAsync(owner, cancellationToken);
        if (current is null)
            return new ReviseProfileResult.NotFound();
        if (current.Version != request.ExpectedVersion)
            return new ReviseProfileResult.Conflict();

        var revised = current.Revise(
            request.Headline,
            request.Evidence ?? throw new ArgumentNullException(nameof(request.Evidence)),
            request.Preferences ?? throw new ArgumentNullException(nameof(request.Preferences)));

        return await _store.ReviseAsync(owner, request.ExpectedVersion, revised, cancellationToken);
    }
}
