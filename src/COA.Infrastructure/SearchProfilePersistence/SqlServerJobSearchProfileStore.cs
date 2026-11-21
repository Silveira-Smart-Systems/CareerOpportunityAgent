using COA.Application.Profile;
using COA.Application.SearchProfiles;
using COA.Domain.SearchProfiles;
using COA.Infrastructure.ProfilePersistence;
using Microsoft.EntityFrameworkCore;

namespace COA.Infrastructure.SearchProfilePersistence;

public sealed class SqlServerJobSearchProfileStore(IDbContextFactory<CoaDbContext> contextFactory) : IJobSearchProfileStore
{
    private readonly IDbContextFactory<CoaDbContext> _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    public async Task<JobSearchProfile> CreateAsync(OwnerIdentity owner, JobSearchProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(profile); profile.Validate();
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var ownerEntity = await ResolveOwnerAsync(db, owner, ct);
        if (ownerEntity is null)
        {
            ownerEntity = new ProfileOwnerEntity { OwnerId = Guid.NewGuid(), OwnerKey = OwnerKeyFactory.Create(owner), Issuer = owner.Issuer, Subject = owner.Subject, CreatedAtUtc = DateTime.UtcNow };
            db.ProfileOwners.Add(ownerEntity);
        }
        db.JobSearchProfiles.Add(ToEntity(profile, ownerEntity.OwnerId));
        await db.SaveChangesAsync(ct);
        return profile;
    }
    public async Task<IReadOnlyList<JobSearchProfile>> ListAsync(OwnerIdentity owner, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var ownerEntity = await ResolveOwnerAsync(db, owner, ct);
        if (ownerEntity is null) return [];
        var profiles = await db.JobSearchProfiles.AsNoTracking().Include(profile => profile.Values)
            .Where(profile => profile.OwnerId == ownerEntity.OwnerId).OrderBy(profile => profile.Name).ThenBy(profile => profile.JobSearchProfileId).ToListAsync(ct);
        return profiles.Select(ToDomain).ToArray();
    }
    public async Task<JobSearchProfile?> GetAsync(OwnerIdentity owner, Guid id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await FindAsync(db, owner, id, false, ct);
        return entity is null ? null : ToDomain(entity);
    }
    public async Task<SearchProfileMutationResult> UpdateAsync(OwnerIdentity owner, Guid id, int expectedVersion, Func<JobSearchProfile, JobSearchProfile> mutate, CancellationToken ct = default)
    {
        if (expectedVersion < 1) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await FindAsync(db, owner, id, true, ct);
        if (entity is null) return new SearchProfileMutationResult.NotFound();
        if (entity.Version != expectedVersion) return new SearchProfileMutationResult.Conflict();
        var profile = mutate(ToDomain(entity));
        if (profile.Id != id || profile.Version != checked(expectedVersion + 1)) throw new ArgumentException("Search profile mutation is invalid.", nameof(mutate));
        Apply(db, profile, entity);
        try { await db.SaveChangesAsync(ct); return new SearchProfileMutationResult.Changed(profile); }
        catch (DbUpdateConcurrencyException) { return new SearchProfileMutationResult.Conflict(); }
    }
    public async Task<SearchProfileMutationResult> DeleteAsync(OwnerIdentity owner, int expectedVersion, Guid id, CancellationToken ct = default)
    {
        if (expectedVersion < 1) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await FindAsync(db, owner, id, true, ct);
        if (entity is null) return new SearchProfileMutationResult.NotFound();
        if (entity.Version != expectedVersion) return new SearchProfileMutationResult.Conflict();
        db.JobSearchProfiles.Remove(entity);
        try { await db.SaveChangesAsync(ct); return new SearchProfileMutationResult.Deleted(); }
        catch (DbUpdateConcurrencyException) { return new SearchProfileMutationResult.Conflict(); }
    }
    private static async Task<JobSearchProfileEntity?> FindAsync(CoaDbContext db, OwnerIdentity owner, Guid id, bool tracked, CancellationToken ct)
    {
        var ownerEntity = await ResolveOwnerAsync(db, owner, ct);
        if (ownerEntity is null) return null;
        var query = db.JobSearchProfiles.Include(profile => profile.Values).Where(profile => profile.OwnerId == ownerEntity.OwnerId && profile.JobSearchProfileId == id);
        return tracked ? await query.SingleOrDefaultAsync(ct) : await query.AsNoTracking().SingleOrDefaultAsync(ct);
    }
    private static async Task<ProfileOwnerEntity?> ResolveOwnerAsync(CoaDbContext db, OwnerIdentity owner, CancellationToken ct)
    {
        var entity = await db.ProfileOwners.SingleOrDefaultAsync(candidate => candidate.OwnerKey == OwnerKeyFactory.Create(owner), ct);
        if (entity is not null && (!string.Equals(entity.Issuer, owner.Issuer, StringComparison.Ordinal) || !string.Equals(entity.Subject, owner.Subject, StringComparison.Ordinal)))
            throw new OwnerKeyCollisionException();
        return entity;
    }
    private static JobSearchProfileEntity ToEntity(JobSearchProfile profile, Guid ownerId)
    {
        var entity = new JobSearchProfileEntity { JobSearchProfileId = profile.Id, OwnerId = ownerId };
        Apply(null, profile, entity); return entity;
    }
    private static void Apply(CoaDbContext? db, JobSearchProfile profile, JobSearchProfileEntity entity)
    {
        entity.Version = profile.Version; entity.Name = profile.Name; entity.IsActive = profile.IsActive; entity.CreatedAtUtc = profile.CreatedAtUtc; entity.UpdatedAtUtc = profile.UpdatedAtUtc;
        entity.RemoteFromCountry = profile.Preferences.RemoteFromCountry;
        entity.MinimumSalary = profile.Preferences.Salary?.Minimum; entity.MaximumSalary = profile.Preferences.Salary?.Maximum;
        entity.SalaryCurrency = profile.Preferences.Salary?.Currency?.ToUpperInvariant(); entity.SalaryPeriod = profile.Preferences.Salary?.Period is { } period ? (byte)period : null;
        var desired = Values(profile).ToHashSet();
        var obsolete = entity.Values.Where(value => !desired.Contains((value.Kind, value.Value))).ToArray();
        foreach (var value in obsolete) { entity.Values.Remove(value); db?.JobSearchProfileValues.Remove(value); }
        var existing = entity.Values.Select(value => (value.Kind, value.Value)).ToHashSet();
        foreach (var (kind, value) in desired.Where(value => !existing.Contains(value))) entity.Values.Add(new JobSearchProfileValueEntity { JobSearchProfileId = entity.JobSearchProfileId, Kind = kind, Value = value });
    }
    private static IEnumerable<(JobSearchProfileValueKind Kind, string Value)> Values(JobSearchProfile profile)
    {
        foreach (var value in profile.Preferences.Categories) yield return (JobSearchProfileValueKind.Category, value);
        foreach (var value in profile.Preferences.TargetRoles) yield return (JobSearchProfileValueKind.TargetRole, value);
        foreach (var value in profile.Preferences.WorkModes) yield return (JobSearchProfileValueKind.WorkMode, value.ToString());
        foreach (var value in profile.Preferences.SourceMarkets) yield return (JobSearchProfileValueKind.SourceMarket, value);
        foreach (var value in profile.Preferences.EmploymentTypes) yield return (JobSearchProfileValueKind.EmploymentType, value);
        foreach (var value in profile.Preferences.Languages) yield return (JobSearchProfileValueKind.Language, value);
        foreach (var value in profile.Preferences.Skills) yield return (JobSearchProfileValueKind.Skill, value);
        foreach (var value in profile.Preferences.ExcludedKeywords) yield return (JobSearchProfileValueKind.ExcludedKeyword, value);
        foreach (var value in profile.Preferences.MandatoryCriteria) yield return (JobSearchProfileValueKind.MandatoryCriterion, value.ToString());
        foreach (var value in profile.Preferences.PreferredCriteria) yield return (JobSearchProfileValueKind.PreferredCriterion, value.ToString());
    }
    private static JobSearchProfile ToDomain(JobSearchProfileEntity entity)
    {
        IReadOnlyList<string> Values(JobSearchProfileValueKind kind) => entity.Values.Where(value => value.Kind == kind).Select(value => value.Value).ToArray();
        var preferences = new JobSearchProfilePreferences(Values(JobSearchProfileValueKind.Category), Values(JobSearchProfileValueKind.TargetRole), Values(JobSearchProfileValueKind.WorkMode).Select(Enum.Parse<SearchWorkMode>).ToArray(), Values(JobSearchProfileValueKind.SourceMarket), entity.RemoteFromCountry, Values(JobSearchProfileValueKind.EmploymentType), entity.MinimumSalary.HasValue || entity.MaximumSalary.HasValue ? new SalaryPreference(entity.MinimumSalary, entity.MaximumSalary, entity.SalaryCurrency, entity.SalaryPeriod is { } period ? (SalaryPeriod)period : null) : null, Values(JobSearchProfileValueKind.Language), Values(JobSearchProfileValueKind.Skill), Values(JobSearchProfileValueKind.ExcludedKeyword), Values(JobSearchProfileValueKind.MandatoryCriterion).Select(Enum.Parse<SearchProfileCriterion>).ToArray(), Values(JobSearchProfileValueKind.PreferredCriterion).Select(Enum.Parse<SearchProfileCriterion>).ToArray());
        var profile = new JobSearchProfile(entity.JobSearchProfileId, entity.Version, entity.Name, entity.IsActive, DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc), DateTime.SpecifyKind(entity.UpdatedAtUtc, DateTimeKind.Utc), preferences);
        profile.Validate(); return profile;
    }
}
