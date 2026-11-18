using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using COA.Application.Profile;

namespace COA.Infrastructure.ProfilePersistence;

internal static class OwnerKeyFactory
{
    public static byte[] Create(OwnerIdentity owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var issuer = Encoding.UTF8.GetBytes(owner.Issuer);
        var subject = Encoding.UTF8.GetBytes(owner.Subject);
        var payload = new byte[sizeof(int) + issuer.Length + sizeof(int) + subject.Length];
        var offset = 0;

        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, sizeof(int)), issuer.Length);
        offset += sizeof(int);
        issuer.CopyTo(payload, offset);
        offset += issuer.Length;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, sizeof(int)), subject.Length);
        offset += sizeof(int);
        subject.CopyTo(payload, offset);

        return SHA256.HashData(payload);
    }
}
