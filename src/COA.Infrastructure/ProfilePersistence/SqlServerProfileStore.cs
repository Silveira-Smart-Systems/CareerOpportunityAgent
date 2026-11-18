using System.Data;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using COA.Application.Profile;
using COA.Domain.Profile;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace COA.Infrastructure.ProfilePersistence;

public sealed class SqlServerProfileStore(IDbContextFactory<CoaDbContext> contextFactory) : IProfileStore
{
    private readonly IDbContextFactory<CoaDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<CreateProfileResult> CreateAsync(
        OwnerIdentity owner,
        CareerProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if (profile.Version != 1)
            throw new ArgumentException("A newly persisted profile must start at version one.", nameof(profile));

        var ownerKey = OwnerKeyFactory.Create(owner);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var ownerEntity = await FindOwnerByKeyAsync(context, ownerKey, cancellationToken);
            Guid ownerId;
            if (ownerEntity is null)
            {
                ownerId = Guid.NewGuid();
                context.ProfileOwners.Add(new ProfileOwnerEntity
                {
                    OwnerId = ownerId,
                    OwnerKey = ownerKey,
                    Issuer = owner.Issuer,
                    Subject = owner.Subject,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                VerifyOwnerTuple(ownerEntity, owner);
                ownerId = ownerEntity.OwnerId;
                if (await context.Profiles.AnyAsync(
                    candidate => candidate.OwnerId == ownerId,
                    cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new CreateProfileResult.Conflict();
                }
            }

            var now = DateTime.UtcNow;
            context.Profiles.Add(new ProfileEntity
            {
                ProfileId = profile.Id,
                OwnerId = ownerId,
                CurrentVersion = profile.Version,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            context.ProfileVersions.Add(CreateVersionEntity(profile, now));

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CreateProfileResult.Created(profile);
        }
        catch (DbUpdateException exception)
            when (TryGetExpectedCreateConstraint(exception, out var constraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return await VerifyCreateConflictWithFreshContextAsync(
                owner,
                ownerKey,
                constraintName,
                exception,
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CareerProfile?> GetCurrentAsync(
        OwnerIdentity owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ownerEntity = await ResolveOwnerAsync(context, owner, cancellationToken);
        if (ownerEntity is null)
            return null;

        var version = await context.ProfileVersions
            .AsNoTracking()
            .Include(snapshot => snapshot.Evidence)
            .SingleOrDefaultAsync(
                snapshot => snapshot.Profile.OwnerId == ownerEntity.OwnerId &&
                    snapshot.Profile.CurrentVersion == snapshot.Version,
                cancellationToken);

        return version is null ? null : ToDomain(version);
    }

    public async Task<CareerProfile?> GetVersionAsync(
        OwnerIdentity owner,
        int version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be positive.");

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ownerEntity = await ResolveOwnerAsync(context, owner, cancellationToken);
        if (ownerEntity is null)
            return null;

        var snapshot = await context.ProfileVersions
            .AsNoTracking()
            .Include(candidate => candidate.Evidence)
            .SingleOrDefaultAsync(
                candidate => candidate.Profile.OwnerId == ownerEntity.OwnerId &&
                    candidate.Version == version,
                cancellationToken);

        return snapshot is null ? null : ToDomain(snapshot);
    }

    public async Task<ReviseProfileResult> ReviseAsync(
        OwnerIdentity owner,
        int expectedVersion,
        CareerProfile revisedProfile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(revisedProfile);
        if (expectedVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected version must be positive.");
        if (revisedProfile.Version != checked(expectedVersion + 1))
            throw new ArgumentException("The revised profile version must follow the expected version.", nameof(revisedProfile));
        revisedProfile.Validate();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var ownerEntity = await ResolveOwnerAsync(context, owner, cancellationToken);
            if (ownerEntity is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ReviseProfileResult.NotFound();
            }

            var now = DateTime.UtcNow;
            var updated = await context.Profiles
                .Where(profile => profile.OwnerId == ownerEntity.OwnerId &&
                    profile.ProfileId == revisedProfile.Id &&
                    profile.CurrentVersion == expectedVersion)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(profile => profile.CurrentVersion, revisedProfile.Version)
                    .SetProperty(profile => profile.UpdatedAtUtc, now),
                    cancellationToken);

            if (updated == 0)
            {
                var submittedProfileExists = await context.Profiles
                    .AsNoTracking()
                    .AnyAsync(profile => profile.OwnerId == ownerEntity.OwnerId &&
                        profile.ProfileId == revisedProfile.Id,
                        cancellationToken);
                await transaction.RollbackAsync(cancellationToken);
                return submittedProfileExists
                    ? new ReviseProfileResult.Conflict()
                    : new ReviseProfileResult.NotFound();
            }

            context.ProfileVersions.Add(CreateVersionEntity(revisedProfile, now));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ReviseProfileResult.Revised(revisedProfile);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<CreateProfileResult> VerifyCreateConflictWithFreshContextAsync(
        OwnerIdentity owner,
        byte[] ownerKey,
        string constraintName,
        DbUpdateException originalException,
        CancellationToken cancellationToken)
    {
        await using var verificationContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var storedOwner = await verificationContext.ProfileOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OwnerKey == ownerKey, cancellationToken);

        if (storedOwner is not null && !OwnerTupleMatches(storedOwner, owner))
            throw new OwnerKeyCollisionException(originalException);

        var fullyCommittedProfile = storedOwner is not null &&
            await verificationContext.Profiles
                .AsNoTracking()
                .AnyAsync(profile => profile.OwnerId == storedOwner.OwnerId, cancellationToken);

        if (fullyCommittedProfile &&
            (constraintName == ProfileSchema.OwnerKeyIndex ||
             constraintName == ProfileSchema.ProfileOwnerIndex))
        {
            return new CreateProfileResult.Conflict();
        }

        ExceptionDispatchInfo.Capture(originalException).Throw();
        throw new UnreachableException();
    }

    private static async Task<ProfileOwnerEntity?> ResolveOwnerAsync(
        CoaDbContext context,
        OwnerIdentity owner,
        CancellationToken cancellationToken)
    {
        var ownerEntity = await FindOwnerByKeyAsync(
            context,
            OwnerKeyFactory.Create(owner),
            cancellationToken);
        if (ownerEntity is not null)
            VerifyOwnerTuple(ownerEntity, owner);
        return ownerEntity;
    }

    private static Task<ProfileOwnerEntity?> FindOwnerByKeyAsync(
        CoaDbContext context,
        byte[] ownerKey,
        CancellationToken cancellationToken) =>
        context.ProfileOwners.SingleOrDefaultAsync(
            candidate => candidate.OwnerKey == ownerKey,
            cancellationToken);

    private static void VerifyOwnerTuple(ProfileOwnerEntity storedOwner, OwnerIdentity requestedOwner)
    {
        if (!OwnerTupleMatches(storedOwner, requestedOwner))
            throw new OwnerKeyCollisionException();
    }

    private static bool OwnerTupleMatches(ProfileOwnerEntity storedOwner, OwnerIdentity requestedOwner) =>
        string.Equals(storedOwner.Issuer, requestedOwner.Issuer, StringComparison.Ordinal) &&
        string.Equals(storedOwner.Subject, requestedOwner.Subject, StringComparison.Ordinal);

    private static ProfileVersionEntity CreateVersionEntity(CareerProfile profile, DateTime createdAtUtc)
    {
        var version = new ProfileVersionEntity
        {
            ProfileId = profile.Id,
            Version = profile.Version,
            Headline = profile.Headline,
            PreferredArrangement = profile.Preferences.PreferredArrangement,
            RemoteOnly = profile.Preferences.RemoteOnly,
            MinimumHourlyRate = profile.Preferences.MinimumHourlyRate,
            Currency = profile.Preferences.Currency,
            MaximumOfficeDaysPerWeek = profile.Preferences.MaximumOfficeDaysPerWeek.HasValue
                ? checked((byte)profile.Preferences.MaximumOfficeDaysPerWeek.Value)
                : null,
            CreatedAtUtc = createdAtUtc
        };

        for (var ordinal = 0; ordinal < profile.Evidence.Count; ordinal++)
        {
            var evidence = profile.Evidence[ordinal];
            version.Evidence.Add(new ProfileEvidenceEntity
            {
                ProfileId = profile.Id,
                Version = profile.Version,
                Ordinal = ordinal,
                Claim = evidence.Claim,
                SourceReference = evidence.SourceReference,
                Origin = evidence.Origin
            });
        }

        return version;
    }

    private static CareerProfile ToDomain(ProfileVersionEntity snapshot)
    {
        var evidence = snapshot.Evidence
            .OrderBy(item => item.Ordinal)
            .Select(item => new ProfileEvidence(item.Claim, item.SourceReference, item.Origin))
            .ToArray();
        var preferences = new CareerPreferences(
            snapshot.PreferredArrangement,
            snapshot.RemoteOnly,
            snapshot.MinimumHourlyRate,
            snapshot.Currency,
            snapshot.MaximumOfficeDaysPerWeek);
        var profile = new CareerProfile(
            snapshot.ProfileId,
            snapshot.Version,
            snapshot.Headline,
            evidence,
            preferences);
        profile.Validate();
        return profile;
    }

    private static bool TryGetExpectedCreateConstraint(
        DbUpdateException exception,
        out string constraintName)
    {
        var sqlException = FindSqlException(exception);
        if (sqlException is null)
        {
            constraintName = string.Empty;
            return false;
        }

        var uniqueErrors = sqlException.Errors.Cast<SqlError>()
            .Where(error => error.Number is 2601 or 2627)
            .ToArray();
        if (uniqueErrors.Length != 1)
        {
            constraintName = string.Empty;
            return false;
        }

        // SqlException has no structured constraint-name property. Restrict message parsing to one
        // SQL Server duplicate-key error and one migration-owned name; localization or an unknown
        // driver format therefore fails closed and propagates the original database exception.
        var expectedConstraints = new[] { ProfileSchema.OwnerKeyIndex, ProfileSchema.ProfileOwnerIndex };
        var matchingConstraints = expectedConstraints
            .Where(candidate => uniqueErrors[0].Message.Contains(candidate, StringComparison.Ordinal))
            .ToArray();
        if (matchingConstraints.Length != 1)
        {
            constraintName = string.Empty;
            return false;
        }

        constraintName = matchingConstraints[0];
        return true;
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException)
                return sqlException;
        }

        return null;
    }
}

public sealed class OwnerKeyCollisionException : InvalidOperationException
{
    internal OwnerKeyCollisionException()
        : base("An owner identity key collision was detected.") { }

    internal OwnerKeyCollisionException(Exception innerException)
        : base("An owner identity key collision was detected.", innerException) { }
}
