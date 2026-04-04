namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for a reader that can parse a package header from a <see cref="Stream"/>.
/// </summary>
public interface IStreamPackageHeaderReader
{
    /// <summary>
    /// Reads the header from a package file.
    /// </summary>
    /// <param name="packageStream">The stream representing the package file to read from.</param>
    /// <returns>The package header that was read.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached before reading the header.</exception>
    /// <exception cref="FormatException">The package header is invalid.</exception>
    /// <exception cref="InvalidDataException">Stream does not contain enough data.</exception>
    /// <exception cref="IOException">An I/O error occurs.</exception>
    /// <exception cref="NotSupportedException">
    /// A class derived from Stream does not support seeking and the length is unknown.
    /// </exception>
    PackageHeader ReadHeader(Stream packageStream);
}