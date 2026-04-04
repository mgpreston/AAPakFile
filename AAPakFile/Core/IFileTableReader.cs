namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for reading the file table from a <see cref="Stream"/> representing a package file.
/// </summary>
public interface IFileTableReader
{
    /// <summary>
    /// Asynchronously reads the file table of a package file, returning the file records as they become available.
    /// </summary>
    /// <param name="stream">The stream representing the package file to read from.</param>
    /// <param name="header">The header of the package file.</param>
    /// <param name="includeExtraFiles">Whether to include records belonging to deleted extra files.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous enumeration of the file records read from the package.</returns>
    /// <exception cref="IOException">An I/O error occurs.</exception>
    /// <exception cref="NotSupportedException"> The stream does not support seeking.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
    IAsyncEnumerable<PackedFileRecord> ReadFileRecordsAsync(Stream stream, PackageHeader header, bool includeExtraFiles,
        CancellationToken cancellationToken);
}