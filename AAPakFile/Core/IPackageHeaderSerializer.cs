namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for serializing a package header to an encrypted binary representation.
/// </summary>
public interface IPackageHeaderSerializer
{
    /// <summary>
    /// Serializes the given package header and writes the encrypted result into <paramref name="destination"/>.
    /// </summary>
    /// <param name="header">The package header to serialize.</param>
    /// <param name="fileCount">The number of active files in the package.</param>
    /// <param name="extraFileCount">The number of deleted (extra) files in the package.</param>
    /// <param name="destination">
    /// A span of exactly 32 bytes to receive the encrypted header. Callers append 480 zero bytes to form the
    /// complete 512-byte header block written at the end of the package file.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is not exactly 32 bytes.
    /// </exception>
    void Serialize(PackageHeader header, int fileCount, int extraFileCount, Span<byte> destination);
}