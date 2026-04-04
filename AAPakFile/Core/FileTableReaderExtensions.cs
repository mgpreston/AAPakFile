using System.Buffers;

namespace AAPakFile.Core;

/// <summary>
/// Extension methods for <see cref="FileTableReader"/>.
/// </summary>
public static class FileTableReaderExtensions
{
    /// <summary>
    /// Asynchronously searches for the file record with the given name.
    /// </summary>
    /// <param name="reader">The file table reader to use.</param>
    /// <param name="stream">The stream containing the package data.</param>
    /// <param name="header">The package header.</param>
    /// <param name="fileName">The name of the file to locate.</param>
    /// <param name="stringComparison">An enumeration value that determines how file names are compared.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> whose result is the matching file record, if found; otherwise, <c>null</c>.
    /// </returns>
    /// <exception cref="IOException">An I/O error occurs.</exception>
    /// <exception cref="NotSupportedException">The stream does not support seeking.</exception>
    /// <exception cref="OperationCanceledException">
    /// The cancellation token was canceled. This exception is stored into the returned task.
    /// </exception>
    /// <remarks>This method does not consider the deleted extra files during its search.</remarks>
    public static async Task<PackedFileRecord?> FindFileRecordAsync(this IFileTableReader reader, Stream stream,
        PackageHeader header, string fileName, StringComparison stringComparison = StringComparison.Ordinal,
        CancellationToken cancellationToken = default)
    {
        using var nameBufferOwner = MemoryPool<char>.Shared.Rent(PackedFileRecord.FileNameBuffer.MaxLength);
        var nameBuffer = nameBufferOwner.Memory;

        await foreach (var fileRecord in reader.ReadFileRecordsAsync(stream, header, includeExtraFiles: false,
                           cancellationToken))
        {
            fileRecord.FileName.WriteTo(nameBuffer.Span, out var charsWritten);
            if (nameBuffer[..charsWritten].Span.Equals(fileName, stringComparison))
            {
                return fileRecord;
            }
        }

        return null;
    }
}