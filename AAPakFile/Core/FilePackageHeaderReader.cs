using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Core;

/// <summary>
/// A reader that can parse a package header from a file handle.
/// </summary>
public class FilePackageHeaderReader : IFilePackageHeaderReader
{
    private readonly IPackageHeaderParser _parser;
    private readonly IRandomAccessReader _reader;

    /// <summary>Initialises a new instance using the default <see cref="RandomAccessReader"/>.</summary>
    /// <param name="parser">The parser to use to read the package header.</param>
    public FilePackageHeaderReader(IPackageHeaderParser parser)
        : this(parser, RandomAccessReader.Instance) { }

    /// <summary>Initialises a new instance with an explicit reader (used for testing).</summary>
    /// <param name="parser">The parser to use to read the package header.</param>
    /// <param name="reader">The <see cref="IRandomAccessReader"/> to use for file I/O.</param>
    internal FilePackageHeaderReader(IPackageHeaderParser parser, IRandomAccessReader reader)
    {
        _parser = parser;
        _reader = reader;
    }

    /// <inheritdoc />
    public PackageHeader ReadHeader(SafeFileHandle packageHandle)
    {
        var length = _reader.GetLength(packageHandle);
        var headerPosition = length - PackageFormat.BlockSize;

        if (headerPosition < 0)
        {
            throw new InvalidDataException($"File does not contain enough data. Must contain at least {PackageFormat.BlockSize} bytes.");
        }

        Span<byte> buffer = stackalloc byte[PackageFormat.EncryptedHeaderSize];
        var totalRead = 0;
        while (totalRead < PackageFormat.EncryptedHeaderSize)
        {
            var read = _reader.Read(packageHandle, buffer[totalRead..], headerPosition + totalRead);
            if (read == 0)
            {
                throw new InvalidDataException("Unexpected end of file while reading header");
            }

            totalRead += read;
        }

        return _parser.Parse(buffer);
    }
}