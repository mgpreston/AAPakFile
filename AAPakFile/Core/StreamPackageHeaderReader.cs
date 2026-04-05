namespace AAPakFile.Core;

/// <summary>
/// A reader that can parse the header from a package exposed via <see cref="Stream"/>.
/// </summary>
/// <param name="parser">The parser to use to read the package header.</param>
public class StreamPackageHeaderReader(IPackageHeaderParser parser) : IStreamPackageHeaderReader
{
    /// <inheritdoc />
    public PackageHeader ReadHeader(Stream packageStream)
    {
        // Package file contents are aligned to PackageFormat.BlockSize byte boundaries. The last block contains the header.
        var headerPosition = packageStream.Length - PackageFormat.BlockSize;

        if (headerPosition < 0)
        {
            throw new InvalidDataException($"Stream does not contain enough data. Must contain at least {PackageFormat.BlockSize} bytes.");
        }

        packageStream.Seek(headerPosition, SeekOrigin.Begin);

        Span<byte> buffer = stackalloc byte[PackageFormat.EncryptedHeaderSize];
        packageStream.ReadExactly(buffer);

        return parser.Parse(buffer);
    }
}