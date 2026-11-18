using COA.Domain.Profile;

namespace COA.Infrastructure.ProfilePersistence;

internal sealed class ProfileOwnerEntity
{
    public Guid OwnerId { get; set; }
    public byte[] OwnerKey { get; set; } = [];
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public ProfileEntity? Profile { get; set; }
}

internal sealed class ProfileEntity
{
    public Guid ProfileId { get; set; }
    public Guid OwnerId { get; set; }
    public int CurrentVersion { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ProfileOwnerEntity Owner { get; set; } = null!;
    public List<ProfileVersionEntity> Versions { get; set; } = [];
}

internal sealed class ProfileVersionEntity
{
    public Guid ProfileId { get; set; }
    public int Version { get; set; }
    public string Headline { get; set; } = string.Empty;
    public WorkArrangement PreferredArrangement { get; set; }
    public bool RemoteOnly { get; set; }
    public decimal? MinimumHourlyRate { get; set; }
    public string? Currency { get; set; }
    public byte? MaximumOfficeDaysPerWeek { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ProfileEntity Profile { get; set; } = null!;
    public List<ProfileEvidenceEntity> Evidence { get; set; } = [];
}

internal sealed class ProfileEvidenceEntity
{
    public Guid ProfileId { get; set; }
    public int Version { get; set; }
    public int Ordinal { get; set; }
    public string Claim { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public EvidenceOrigin Origin { get; set; }
    public ProfileVersionEntity ProfileVersion { get; set; } = null!;
}
