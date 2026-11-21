using COA.Domain.SearchProfiles;
using COA.Application.Profile;
namespace COA.Application.SearchProfiles;
public interface IJobSearchProfileStore
{
    Task<JobSearchProfile> CreateAsync(OwnerIdentity owner, JobSearchProfile profile, CancellationToken ct = default);
    Task<IReadOnlyList<JobSearchProfile>> ListAsync(OwnerIdentity owner, CancellationToken ct = default);
    Task<JobSearchProfile?> GetAsync(OwnerIdentity owner, Guid id, CancellationToken ct = default);
    Task<SearchProfileMutationResult> UpdateAsync(OwnerIdentity owner, int expectedVersion, JobSearchProfile profile, CancellationToken ct = default);
    Task<SearchProfileMutationResult> DeleteAsync(OwnerIdentity owner, int expectedVersion, Guid id, CancellationToken ct = default);
}
public abstract record SearchProfileMutationResult { private SearchProfileMutationResult() { } public sealed record Changed(JobSearchProfile Profile) : SearchProfileMutationResult; public sealed record Deleted : SearchProfileMutationResult; public sealed record NotFound : SearchProfileMutationResult; public sealed record Conflict : SearchProfileMutationResult; }
