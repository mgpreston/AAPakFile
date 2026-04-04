namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for writing the file table to a <see cref="Stream"/> representing a package file.
/// </summary>
public interface IFileTableWriter
{
    /// <summary>
    /// Asynchronously writes the given file records to the stream, encrypting each record individually.
    /// </summary>
    /// <param name="stream">The stream to write the encrypted file records to.</param>
    /// <param name="records">The file records to write.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> that completes when all records have been written.</returns>
    /// <exception cref="IOException">An I/O error occurs.</exception>
    /// <exception cref="NotSupportedException">The stream does not support writing.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
    Task WriteFileRecordsAsync(Stream stream, IEnumerable<PackedFileRecord> records,
        CancellationToken cancellationToken);
}