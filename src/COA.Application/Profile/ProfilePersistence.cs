using COA.Domain.Profile;

namespace COA.Application.Profile;

public interface IProfileStore
{
    Task<CreateProfileResult> CreateAsync(
        OwnerIdentity owner,
        CareerProfile profile,
        CancellationToken cancellationToken = default);

    Task<CareerProfile?> GetCurrentAsync(
        OwnerIdentity owner,
        CancellationToken cancellationToken = default);

    Task<CareerProfile?> GetVersionAsync(
        OwnerIdentity owner,
        int version,
        CancellationToken cancellationToken = default);

    Task<ReviseProfileResult> ReviseAsync(
        OwnerIdentity owner,
        int expectedVersion,
        CareerProfile revisedProfile,
        CancellationToken cancellationToken = default);
}

public abstract record CreateProfileResult
{
    private CreateProfileResult() { }

    public sealed record Created(CareerProfile Profile) : CreateProfileResult;

    public sealed record Conflict : CreateProfileResult;
}

public abstract record ProfileReadResult
{
    private ProfileReadResult() { }

    public sealed record Found(CareerProfile Profile) : ProfileReadResult;

    public sealed record NotFound : ProfileReadResult;
}

public abstract record ReviseProfileResult
{
    private ReviseProfileResult() { }

    public sealed record Revised(CareerProfile Profile) : ReviseProfileResult;

    public sealed record NotFound : ReviseProfileResult;

    public sealed record Conflict : ReviseProfileResult;
}
