using COA.Application.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace COA.Infrastructure.ProfilePersistence;

internal static class ProfileSchema
{
    public const string BinaryCollation = "Latin1_General_100_BIN2";
    public const string OwnerKeyIndex = "UX_ProfileOwners_OwnerKey";
    public const string ProfileOwnerIndex = "UX_Profiles_OwnerId";
}

internal sealed class ProfileOwnerConfiguration : IEntityTypeConfiguration<ProfileOwnerEntity>
{
    public void Configure(EntityTypeBuilder<ProfileOwnerEntity> builder)
    {
        builder.ToTable("ProfileOwners");
        builder.HasKey(owner => owner.OwnerId).HasName("PK_ProfileOwners");
        builder.Property(owner => owner.OwnerKey).HasColumnType("binary(32)").IsRequired();
        builder.Property(owner => owner.Issuer)
            .HasMaxLength(OwnerIdentity.MaximumIdentifierLength)
            .UseCollation(ProfileSchema.BinaryCollation)
            .IsRequired();
        builder.Property(owner => owner.Subject)
            .HasMaxLength(OwnerIdentity.MaximumIdentifierLength)
            .UseCollation(ProfileSchema.BinaryCollation)
            .IsRequired();
        builder.Property(owner => owner.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(owner => owner.OwnerKey)
            .IsUnique()
            .HasDatabaseName(ProfileSchema.OwnerKeyIndex);
    }
}

internal sealed class JobSearchProfileConfiguration : IEntityTypeConfiguration<JobSearchProfileEntity>
{
    public void Configure(EntityTypeBuilder<JobSearchProfileEntity> builder)
    {
        builder.ToTable("JobSearchProfiles");
        builder.HasKey(profile => profile.JobSearchProfileId).HasName("PK_JobSearchProfiles");
        builder.Property(profile => profile.Name).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.RemoteFromCountry).HasMaxLength(2).IsFixedLength();
        builder.Property(profile => profile.MinimumSalary).HasPrecision(18, 2);
        builder.Property(profile => profile.MaximumSalary).HasPrecision(18, 2);
        builder.Property(profile => profile.SalaryCurrency).HasMaxLength(3).IsFixedLength();
        builder.Property(profile => profile.RowVersion).IsRowVersion();
        builder.Property(profile => profile.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(profile => profile.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(profile => new { profile.OwnerId, profile.IsActive });
        builder.HasOne(profile => profile.Owner).WithMany(owner => owner.JobSearchProfiles).HasForeignKey(profile => profile.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class JobSearchProfileValueConfiguration : IEntityTypeConfiguration<JobSearchProfileValueEntity>
{
    public void Configure(EntityTypeBuilder<JobSearchProfileValueEntity> builder)
    {
        builder.ToTable("JobSearchProfileValues");
        builder.HasKey(value => new { value.JobSearchProfileId, value.Kind, value.Value }).HasName("PK_JobSearchProfileValues");
        builder.Property(value => value.Value).HasMaxLength(200).UseCollation("Latin1_General_100_CI_AS");
        builder.HasOne(value => value.Profile).WithMany(profile => profile.Values).HasForeignKey(value => value.JobSearchProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProfileConfiguration : IEntityTypeConfiguration<ProfileEntity>
{
    public void Configure(EntityTypeBuilder<ProfileEntity> builder)
    {
        builder.ToTable("Profiles", table =>
            table.HasCheckConstraint("CK_Profiles_CurrentVersion", "[CurrentVersion] >= 1"));
        builder.HasKey(profile => profile.ProfileId).HasName("PK_Profiles");
        builder.Property(profile => profile.CurrentVersion).IsRequired();
        builder.Property(profile => profile.RowVersion).IsRowVersion();
        builder.Property(profile => profile.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(profile => profile.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(profile => profile.OwnerId)
            .IsUnique()
            .HasDatabaseName(ProfileSchema.ProfileOwnerIndex);
        builder.HasOne(profile => profile.Owner)
            .WithOne(owner => owner.Profile)
            .HasForeignKey<ProfileEntity>(profile => profile.OwnerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Profiles_ProfileOwners_OwnerId");
    }
}

internal sealed class ProfileVersionConfiguration : IEntityTypeConfiguration<ProfileVersionEntity>
{
    public void Configure(EntityTypeBuilder<ProfileVersionEntity> builder)
    {
        builder.ToTable("ProfileVersions", table =>
        {
            table.HasCheckConstraint("CK_ProfileVersions_Version", "[Version] >= 1");
            table.HasCheckConstraint("CK_ProfileVersions_PreferredArrangement", "[PreferredArrangement] BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_ProfileVersions_MinimumHourlyRate", "[MinimumHourlyRate] IS NULL OR ([MinimumHourlyRate] >= 0 AND [MinimumHourlyRate] <= 100000)");
            table.HasCheckConstraint("CK_ProfileVersions_MaximumOfficeDays", "[MaximumOfficeDaysPerWeek] IS NULL OR [MaximumOfficeDaysPerWeek] BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_ProfileVersions_RateCurrency", "[MinimumHourlyRate] IS NULL OR [Currency] IS NOT NULL");
            table.HasCheckConstraint("CK_ProfileVersions_CurrencyLength", "[Currency] IS NULL OR LEN([Currency]) = 3");
            table.HasCheckConstraint("CK_ProfileVersions_RemoteOnly", "[RemoteOnly] = 0 OR ([PreferredArrangement] = 0 AND ([MaximumOfficeDaysPerWeek] IS NULL OR [MaximumOfficeDaysPerWeek] = 0))");
        });
        builder.HasKey(version => new { version.ProfileId, version.Version }).HasName("PK_ProfileVersions");
        builder.Property(version => version.Headline).HasMaxLength(200).IsRequired();
        builder.Property(version => version.PreferredArrangement).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(version => version.RemoteOnly).IsRequired();
        builder.Property(version => version.MinimumHourlyRate).HasPrecision(18, 2);
        builder.Property(version => version.Currency).HasColumnType("char(3)").IsFixedLength().HasMaxLength(3);
        builder.Property(version => version.MaximumOfficeDaysPerWeek).HasColumnType("tinyint");
        builder.Property(version => version.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasOne(version => version.Profile)
            .WithMany(profile => profile.Versions)
            .HasForeignKey(version => version.ProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProfileVersions_Profiles_ProfileId");
    }
}

internal sealed class ProfileEvidenceConfiguration : IEntityTypeConfiguration<ProfileEvidenceEntity>
{
    public void Configure(EntityTypeBuilder<ProfileEvidenceEntity> builder)
    {
        builder.ToTable("ProfileEvidence", table =>
        {
            table.HasCheckConstraint("CK_ProfileEvidence_Ordinal", "[Ordinal] BETWEEN 0 AND 199");
            table.HasCheckConstraint("CK_ProfileEvidence_Origin", "[Origin] BETWEEN 0 AND 2");
        });
        builder.HasKey(evidence => new { evidence.ProfileId, evidence.Version, evidence.Ordinal })
            .HasName("PK_ProfileEvidence");
        builder.Property(evidence => evidence.Claim).HasMaxLength(500).IsRequired();
        builder.Property(evidence => evidence.SourceReference).HasMaxLength(300).IsRequired();
        builder.Property(evidence => evidence.Origin).HasConversion<byte>().HasColumnType("tinyint");
        builder.HasOne(evidence => evidence.ProfileVersion)
            .WithMany(version => version.Evidence)
            .HasForeignKey(evidence => new { evidence.ProfileId, evidence.Version })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProfileEvidence_ProfileVersions_ProfileId_Version");
    }
}

internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<COA.Infrastructure.OpportunityPersistence.OpportunityEntity>
{ public void Configure(EntityTypeBuilder<COA.Infrastructure.OpportunityPersistence.OpportunityEntity> b){b.ToTable("Opportunities");b.HasKey(x=>x.Id);b.Property(x=>x.OwnerKey).HasColumnType("binary(32)").IsRequired();b.Property(x=>x.OriginalUrl).HasMaxLength(2048).IsRequired();b.Property(x=>x.CanonicalUrl).HasMaxLength(2048).IsRequired();b.Property(x=>x.Title).HasMaxLength(200).IsRequired();b.Property(x=>x.Description).HasMaxLength(12000).IsRequired();b.HasIndex(x=>new{x.OwnerKey,x.CanonicalUrl}).IsUnique();} }
