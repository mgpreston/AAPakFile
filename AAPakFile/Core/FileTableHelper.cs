using System.Runtime.CompilerServices;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Core;

/// <summary>
/// Contains static helper methods for loading the file table from a package.
/// </summary>
internal static class FileTableHelper
{
    /// <summary>
    /// Asynchronously loads all file records from the specified package.
    /// </summary>
    /// <param name="packageHandle">The handle to the package.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="fileTableStreamBufferSize">The buffer size of the file stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A collection of file records.</returns>
    public static async Task<IEnumerable<PackedFileRecord>> LoadRecordsAsync(SafeFileHandle packageHandle,
        ReadOnlyMemory<byte> xlGamesKey = default, int fileTableStreamBufferSize = 80 * 1024,
        CancellationToken cancellationToken = default)
    {
        using var decryptor = new Decryptor(xlGamesKey.Span);
        var headerParser = new PackageHeaderParser(decryptor);
        var headerReader = new FilePackageHeaderReader(headerParser);
        var header = headerReader.ReadHeader(packageHandle);

        // Intentionally not disposing the file stream to avoid disposing the package file handle that we don't own
        var packageFileStream = new FileStream(packageHandle, FileAccess.Read, fileTableStreamBufferSize,
            isAsync: true);
        var fileTableReader = new FileTableReader(decryptor);
        var enumerator =
            fileTableReader.ReadFileRecordsAsync(packageFileStream, header, includeExtraFiles: false,
                cancellationToken);
        var records = await enumerator.ToListAsync(cancellationToken).ConfigureAwait(false);
        // Sort ascending by offset so that callers processing records in order get sequential disk reads.
        records.Sort(static (a, b) => a.FileOffset.CompareTo(b.FileOffset));
        return records;
    }

    /// <summary>
    /// Asynchronously loads all state required to edit the specified package.
    /// </summary>
    /// <param name="packageHandle">The handle to the package, opened for read/write access.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="fileTableStreamBufferSize">The buffer size of the file stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="PackageEditState"/> containing all state required for editing.</returns>
    public static async Task<PackageEditState> LoadRecordsForEditingAsync(SafeFileHandle packageHandle,
        ReadOnlyMemory<byte> xlGamesKey = default, int fileTableStreamBufferSize = 80 * 1024,
        CancellationToken cancellationToken = default)
    {
        var fileRecordSize = Unsafe.SizeOf<PackedFileRecord>();

        using var decryptor = new Decryptor(xlGamesKey.Span);
        var headerParser = new PackageHeaderParser(decryptor);
        var headerReader = new FilePackageHeaderReader(headerParser);
        var header = headerReader.ReadHeader(packageHandle);

        // Intentionally not disposing the file stream to avoid disposing the package file handle that we don't own
        var packageFileStream = new FileStream(packageHandle, FileAccess.Read, fileTableStreamBufferSize,
            isAsync: true);
        var fileTableReader = new FileTableReader(decryptor);

        // Load all records including extra (deleted) files so the editor can reuse their space
        var allRecords = await fileTableReader
            .ReadFileRecordsAsync(packageFileStream, header, includeExtraFiles: true, cancellationToken)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Split into active files and extra (deleted) files by their counts from the header
        var files = allRecords[..header.FileCount];
        var extraFiles = allRecords[header.FileCount..];

        // Sort active files by offset ascending for sequential disk access (same as LoadRecordsAsync).
        // ExtraFiles are NOT sorted — their insertion order in the FAT must be preserved.
        files.Sort(static (a, b) => a.FileOffset.CompareTo(b.FileOffset));

        // Compute the offset where new file data should be appended (= current FAT start position),
        // using the same alignment formula as FileTableReader.
        var totalRecordCount = header.FileCount + header.ExtraFileCount;
        var fileLength = RandomAccess.GetLength(packageHandle);
        var firstFileInfoOffset = fileLength - PackageFormat.BlockSize - (long)fileRecordSize * totalRecordCount;
        var dif = firstFileInfoOffset % PackageFormat.BlockSize;
        firstFileInfoOffset -= dif; // Align backward to PackageFormat.BlockSize boundary

        return new PackageEditState(header, files, extraFiles, firstFileInfoOffset);
    }

    /// <summary>
    /// Asynchronously loads all file records from the specified package stream.
    /// </summary>
    /// <param name="packageStream">The stream representing the package.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A collection of file records.</returns>
    public static async Task<IEnumerable<PackedFileRecord>> LoadRecordsAsync(Stream packageStream,
        ReadOnlyMemory<byte> xlGamesKey = default,
        CancellationToken cancellationToken = default)
    {
        using var decryptor = new Decryptor(xlGamesKey.Span);
        var headerParser = new PackageHeaderParser(decryptor);
        var headerReader = new StreamPackageHeaderReader(headerParser);
        var header = headerReader.ReadHeader(packageStream);

        var fileTableReader = new FileTableReader(decryptor);
        var enumerator =
            fileTableReader.ReadFileRecordsAsync(packageStream, header, includeExtraFiles: false,
                cancellationToken);
        return await enumerator.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}