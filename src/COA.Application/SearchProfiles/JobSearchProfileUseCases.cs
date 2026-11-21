using COA.Application.Profile;
using COA.Domain.SearchProfiles;
namespace COA.Application.SearchProfiles;
public sealed record CreateJobSearchProfileRequest(string Name, JobSearchProfilePreferences Preferences);
public sealed record UpdateJobSearchProfileRequest(int ExpectedVersion, string Name, JobSearchProfilePreferences Preferences);
public sealed class JobSearchProfileUseCases(IJobSearchProfileStore store, TimeProvider? timeProvider = null)
{
    private readonly IJobSearchProfileStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    public Task<JobSearchProfile> CreateAsync(OwnerIdentity owner, CreateJobSearchProfileRequest request, CancellationToken ct = default) =>
        _store.CreateAsync(owner, JobSearchProfile.Create(Guid.NewGuid(), request.Name, request.Preferences, UtcNow()), ct);
    public Task<IReadOnlyList<JobSearchProfile>> ListAsync(OwnerIdentity owner, CancellationToken ct = default) => _store.ListAsync(owner, ct);
    public Task<JobSearchProfile?> GetAsync(OwnerIdentity owner, Guid id, CancellationToken ct = default) => _store.GetAsync(owner, id, ct);
    public Task<SearchProfileMutationResult> UpdateAsync(OwnerIdentity owner, Guid id, UpdateJobSearchProfileRequest request, CancellationToken ct = default) =>
        _store.UpdateAsync(owner, id, request.ExpectedVersion, current => current.Revise(request.Name, request.Preferences, UtcNow()), ct);
    public Task<SearchProfileMutationResult> SetActiveAsync(OwnerIdentity owner, Guid id, int expectedVersion, bool isActive, CancellationToken ct = default) =>
        _store.UpdateAsync(owner, id, expectedVersion, current => current.SetActive(isActive, UtcNow()), ct);
    public Task<SearchProfileMutationResult> DeleteAsync(OwnerIdentity owner, Guid id, int expectedVersion, CancellationToken ct = default) => _store.DeleteAsync(owner, expectedVersion, id, ct);
    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
}
