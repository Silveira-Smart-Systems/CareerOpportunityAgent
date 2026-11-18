namespace COA.Application.Profile;

public sealed class OwnerIdentity : IEquatable<OwnerIdentity>
{
    public const int MaximumIdentifierLength = 2048;

    public OwnerIdentity(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            throw new ArgumentException("Issuer is required.", nameof(issuer));
        if (issuer.Length > MaximumIdentifierLength)
            throw new ArgumentException($"Issuer is limited to {MaximumIdentifierLength} characters.", nameof(issuer));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (subject.Length > MaximumIdentifierLength)
            throw new ArgumentException($"Subject is limited to {MaximumIdentifierLength} characters.", nameof(subject));

        Issuer = issuer;
        Subject = subject;
    }

    public string Issuer { get; }

    public string Subject { get; }

    public bool Equals(OwnerIdentity? other) =>
        other is not null &&
        StringComparer.Ordinal.Equals(Issuer, other.Issuer) &&
        StringComparer.Ordinal.Equals(Subject, other.Subject);

    public override bool Equals(object? obj) => Equals(obj as OwnerIdentity);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(Issuer),
        StringComparer.Ordinal.GetHashCode(Subject));

    public override string ToString() => "OwnerIdentity { Redacted }";
}
