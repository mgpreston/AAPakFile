using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace AAPakFile.Core;

/// <summary>
/// Supports parsing the header of a package from a right-sized buffer.
/// </summary>
/// <param name="decryptor">The decryptor to use to decrypt the package header.</param>
public class PackageHeaderParser(IDecryptor decryptor) : IPackageHeaderParser
{
    /// <inheritdoc />
    public bool TryParse(Span<byte> data, [NotNullWhen(true)] out PackageHeader? header)
    {
        const int headerSize = 32;

        if (data.Length != headerSize)
        {
            header = null;
            return false;
        }

        Span<byte> decrypted = stackalloc byte[headerSize];
        decryptor.Decrypt(data, decrypted);

        if (!decrypted.StartsWith(PackageHeader.HeaderMagic.Span))
        {
            header = null;
            return false;
        }

        (int unknown, uint fileCount, uint extraFileCount) = ReadHeaderValues(decrypted);

        if (fileCount > int.MaxValue || extraFileCount > int.MaxValue)
        {
            header = null;
            return false;
        }

        header = new PackageHeader(unknown, (int)fileCount, (int)extraFileCount);
        return true;
    }

    /// <inheritdoc />
    public PackageHeader Parse(Span<byte> data)
    {
        const int headerSize = 32;

        if (data.Length != headerSize)
        {
            throw new ArgumentException("Data length does not match header size", nameof(data));
        }

        Span<byte> decrypted = stackalloc byte[headerSize];
        decryptor.Decrypt(data, decrypted);

        if (!decrypted.StartsWith(PackageHeader.HeaderMagic.Span))
        {
            throw new FormatException("Invalid header magic");
        }

        (int unknown, uint fileCount, uint extraFileCount) = ReadHeaderValues(decrypted);

        if (fileCount > int.MaxValue)
        {
            throw new FormatException("Package contains too many files");
        }

        if (extraFileCount > int.MaxValue)
        {
            throw new FormatException("Package contains too many extra files");
        }

        return new PackageHeader(unknown, (int)fileCount, (int)extraFileCount);
    }

    private static (int Unknown, uint FileCount, uint ExtraFileCount) ReadHeaderValues(Span<byte> data)
    {
        var unknown = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        var fileCount = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        var extraFileCount = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);

        return (unknown, fileCount, extraFileCount);
    }
}