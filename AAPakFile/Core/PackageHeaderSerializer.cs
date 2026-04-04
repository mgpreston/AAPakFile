using System.Buffers.Binary;

namespace AAPakFile.Core;

/// <summary>
/// Serializes a <see cref="PackageHeader"/> to an encrypted binary representation.
/// </summary>
/// <param name="encryptor">The encryptor to use to encrypt the serialized header.</param>
public class PackageHeaderSerializer(IEncryptor encryptor) : IPackageHeaderSerializer
{
    /// <inheritdoc />
    public void Serialize(PackageHeader header, int fileCount, int extraFileCount, Span<byte> destination)
    {
        const int headerSize = 32;

        if (destination.Length != headerSize)
        {
            throw new ArgumentException($"Destination must be exactly {headerSize} bytes.", nameof(destination));
        }

        Span<byte> plaintext = stackalloc byte[headerSize];
        plaintext.Clear();

        // Write magic bytes at offset 0
        PackageHeader.HeaderMagic.Span.CopyTo(plaintext);

        // Write Unknown at offset 4
        BinaryPrimitives.WriteInt32LittleEndian(plaintext[4..], header.Unknown);

        // Write FileCount at offset 8
        BinaryPrimitives.WriteUInt32LittleEndian(plaintext[8..], (uint)fileCount);

        // Write ExtraFileCount at offset 12
        BinaryPrimitives.WriteUInt32LittleEndian(plaintext[12..], (uint)extraFileCount);

        encryptor.Encrypt(plaintext, destination);
    }
}