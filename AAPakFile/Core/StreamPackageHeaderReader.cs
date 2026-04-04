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
        const int headerSize = 32;

        // Package file contents are aligned to 512 byte boundaries. The last block contains the header.
        var headerPosition = packageStream.Length - 512;

        if (headerPosition < 0)
        {
            throw new InvalidDataException("Stream does not contain enough data. Must contain at least 512 bytes.");
        }

        packageStream.Seek(headerPosition, SeekOrigin.Begin);

        Span<byte> buffer = stackalloc byte[headerSize];
        packageStream.ReadExactly(buffer);

        return parser.Parse(buffer);
    }
}