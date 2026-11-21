using Microsoft.EntityFrameworkCore;
using COA.Infrastructure.OpportunityPersistence;

namespace COA.Infrastructure.ProfilePersistence;

public sealed class CoaDbContext(DbContextOptions<CoaDbContext> options) : DbContext(options)
{
    internal DbSet<ProfileOwnerEntity> ProfileOwners => Set<ProfileOwnerEntity>();
    internal DbSet<ProfileEntity> Profiles => Set<ProfileEntity>();
    internal DbSet<ProfileVersionEntity> ProfileVersions => Set<ProfileVersionEntity>();
    internal DbSet<ProfileEvidenceEntity> ProfileEvidence => Set<ProfileEvidenceEntity>();
    internal DbSet<OpportunityEntity> Opportunities => Set<OpportunityEntity>();
    internal DbSet<JobSearchProfileEntity> JobSearchProfiles => Set<JobSearchProfileEntity>();
    internal DbSet<JobSearchProfileValueEntity> JobSearchProfileValues => Set<JobSearchProfileValueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoaDbContext).Assembly);
    }
}
