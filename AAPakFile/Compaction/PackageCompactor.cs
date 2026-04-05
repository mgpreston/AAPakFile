using System.Buffers;

using AAPakFile.Core;
using AAPakFile.Editing;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Compaction;

/// <summary>
/// Provides methods for compacting a package file by removing unused gaps between files.
/// </summary>
public class PackageCompactor
{
    /// <summary>
    /// Rewrites the package to a temporary file in the same directory, removing all unused gaps,
    /// then atomically replaces the original.
    /// </summary>
    /// <remarks>
    /// Requires approximately the same amount of free disk space as the original package.
    /// The original file is never modified if the operation fails or is cancelled.
    /// </remarks>
    /// <param name="packagePath">The path to the package file to compact.</param>
    /// <param name="xlGamesKey">The AES key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task CompactAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default,
        IProgress<CompactProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(packagePath))!;
        var tempPath = Path.Combine(directory, Path.GetRandomFileName() + ".tmp");
        try
        {
            {
                using var sourceHandle = File.OpenHandle(packagePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read, FileOptions.Asynchronous);

                var records = (await FileTableHelper
                    .LoadRecordsAsync(sourceHandle, xlGamesKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false))
                    .ToList();

                await using var editor = await PackageEditor
                    .CreateAsync(tempPath, xlGamesKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                for (var i = 0; i < records.Count; i++)
                {
                    var record = records[i];
                    var options = new PackageWriteOptions
                    {
                        CreationTime = record.CreationTime.AsDateTimeOffset(),
                        ModifiedTime = record.ModifiedTime.AsDateTimeOffset()
                    };
                    // PackedFileStream.CanSeek = true → editor uses seekable path, reads exact length
                    await using var stream = new PackedFileStream(
                        sourceHandle, record.FileOffset, record.FileSize);
                    await editor.AddOrReplaceFileAsync(
                        record.FileName.ToString(), stream, options, cancellationToken)
                        .ConfigureAwait(false);
                    progress?.Report(new CompactProgress(i + 1, records.Count));
                }

                await editor.SaveAsync(cancellationToken).ConfigureAwait(false);
            } // sourceHandle and editor disposed — both handles released

            File.Move(tempPath, packagePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Removes unused gaps by shifting file data in-place within the same file, then rewrites
    /// the file table and truncates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Does not require extra disk space. The data-movement step is safe: records are processed
    /// in ascending offset order and each file is always shifted to a lower offset, so the
    /// write window never overlaps unread source data.
    /// </para>
    /// <para>
    /// <strong>If the operation is interrupted after data has been moved but before the file
    /// table is written, the file will be corrupt and unrecoverable.</strong>
    /// Use <see cref="CompactAsync"/> when data integrity on failure is required.
    /// </para>
    /// </remarks>
    /// <param name="packagePath">The path to the package file to compact.</param>
    /// <param name="xlGamesKey">The AES key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="bufferSize">The buffer size used for data-shift and file-table write operations. Defaults to 80 KiB.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task CompactInPlaceAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default,
        IProgress<CompactProgress>? progress = null,
        int bufferSize = 80 * 1024,
        CancellationToken cancellationToken = default)
    {
        using var handle = File.OpenHandle(packagePath, FileMode.Open,
            FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);

        // Load full editing state: active files sorted by FileOffset + original header
        var state = await FileTableHelper
            .LoadRecordsForEditingAsync(handle, xlGamesKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var records = state.Files; // List<PackedFileRecord> sorted by FileOffset ascending
        var updatedRecords = new List<PackedFileRecord>(records.Count);

        using var bufferOwner = MemoryPool<byte>.Shared.Rent(bufferSize);
        var buffer = bufferOwner.Memory;

        var writeOffset = 0L;
        for (var i = 0; i < records.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = records[i];
            var blockSize = record.FileSize + record.PaddingSize;

            if (record.FileOffset != writeOffset)
            {
                // Shift data left to fill the gap.
                // Destination < source always, so reads and writes never overlap.
                var remaining = blockSize;
                var readPos = record.FileOffset;
                var writePos = writeOffset;
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(remaining, buffer.Length);
                    var read = await RandomAccess
                        .ReadAsync(handle, buffer[..toRead], readPos, cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0) break;
                    await RandomAccess
                        .WriteAsync(handle, buffer[..read], writePos, cancellationToken)
                        .ConfigureAwait(false);
                    readPos += read;
                    writePos += read;
                    remaining -= read;
                }
            }

            updatedRecords.Add(record with { FileOffset = writeOffset });
            writeOffset += blockSize;
            progress?.Report(new CompactProgress(i + 1, records.Count));
        }

        // Write the new file table, header, and truncate.
        using var encryptor = new Encryptor(xlGamesKey.Span);
        await WriteFileTableAsync(handle, writeOffset, updatedRecords, state.Header,
            encryptor, bufferSize, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteFileTableAsync(SafeFileHandle handle, long fileTableOffset,
        List<PackedFileRecord> records, PackageHeader header, IEncryptor encryptor,
        int streamBufferSize, CancellationToken cancellationToken)
    {
        var fileTableWriter = new FileTableWriter(encryptor);
        var headerSerializer = new PackageHeaderSerializer(encryptor);

        // Intentionally not disposing: we don't own the handle
        var stream = new FileStream(handle, FileAccess.Write, bufferSize: streamBufferSize, isAsync: true);
        stream.Seek(fileTableOffset, SeekOrigin.Begin);

        await fileTableWriter.WriteFileRecordsAsync(stream, records, cancellationToken).ConfigureAwait(false);
        await fileTableWriter.WriteFileRecordsAsync(stream, [], cancellationToken).ConfigureAwait(false);

        var dif = stream.Position % PackageFormat.BlockSize;
        if (dif > 0)
            await stream.WriteAsync(new byte[PackageFormat.BlockSize - dif], cancellationToken).ConfigureAwait(false);

        Span<byte> encryptedHeader = stackalloc byte[PackageFormat.EncryptedHeaderSize];
        headerSerializer.Serialize(header, records.Count, 0, encryptedHeader);
        await stream.WriteAsync(encryptedHeader.ToArray(), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(new byte[PackageFormat.BlockSize - PackageFormat.EncryptedHeaderSize], cancellationToken).ConfigureAwait(false);

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.SetLength(stream.Position);
    }
}