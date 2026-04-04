using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using AAPakFile.Core;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Editing;

/// <summary>
/// Provides methods for editing the contents of a package file.
/// </summary>
/// <remarks>
/// <para>
/// Open a <see cref="PackageEditor"/> via <see cref="OpenAsync"/> or
/// <see cref="PackageFile.OpenEditorAsync"/>. Edits are applied in memory and written to the package
/// only when <see cref="SaveAsync"/> is called.
/// </para>
/// <para>
/// File data passed to
/// <see cref="AddOrReplaceFileAsync(string, System.IO.Stream, AAPakFile.Editing.PackageWriteOptions, System.Threading.CancellationToken)"/>
/// is written directly to the package file immediately. Call <see cref="SaveAsync"/> promptly after
/// completing all edits to update the file table and keep the package in a consistent state.
/// </para>
/// <para>
/// This class is not thread-safe. Callers must serialise access.
/// </para>
/// </remarks>
public sealed class PackageEditor : IPackageEditor
{
    private readonly SafeFileHandle _handle;
    private readonly Encryptor _encryptor;
    private readonly IFileTableWriter _fileTableWriter;
    private readonly IPackageHeaderSerializer _headerSerializer;
    private readonly PackageHeader _header;

    private readonly List<PackedFileRecord> _files;
    private readonly List<PackedFileRecord> _extraFiles;
    private readonly DeferrableObservableCollection<PackageEntry> _entriesList;

    private long _firstFileInfoOffset;

    private PackageEditor(SafeFileHandle handle, Encryptor encryptor, IFileTableWriter fileTableWriter,
        IPackageHeaderSerializer headerSerializer, PackageEditState state)
    {
        _handle = handle;
        _encryptor = encryptor;
        _fileTableWriter = fileTableWriter;
        _headerSerializer = headerSerializer;
        _header = state.Header;
        _files = state.Files;
        _extraFiles = state.ExtraFiles;
        _firstFileInfoOffset = state.FirstFileInfoOffset;

        _entriesList = new DeferrableObservableCollection<PackageEntry>(
            _files.Select(r => new PackageEntry(handle, r)));
        Entries = new ReadOnlyObservableCollection<PackageEntry>(_entriesList);
    }

    /// <inheritdoc />
    public bool IsDirty { get; private set; }

    /// <inheritdoc />
    public ReadOnlyObservableCollection<PackageEntry> Entries { get; }

    /// <inheritdoc />
    public IDisposable DeferNotifications() => _entriesList.DeferNotifications();

    /// <summary>
    /// Creates a new, empty package file for editing.
    /// </summary>
    /// <param name="packagePath">
    /// The path to the package file to create. If a file already exists at this path it is overwritten.
    /// </param>
    /// <param name="xlGamesKey">The AES key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="PackageEditor"/> with exclusive read/write access to the new package.</returns>
    /// <exception cref="IOException">An I/O error occurred while creating the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to create or overwrite the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    [SuppressMessage("Style", "IDE0060", Justification = "Public API")]
    [SuppressMessage("ReSharper", "UnusedParameter.Global", Justification = "Public API")]
    public static Task<PackageEditor> CreateAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, CancellationToken cancellationToken = default)
    {
        var handle = File.OpenHandle(packagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous);
        try
        {
            var encryptor = new Encryptor(xlGamesKey.Span);
            var fileTableWriter = new FileTableWriter(encryptor);
            var headerSerializer = new PackageHeaderSerializer(encryptor);
            var state = new PackageEditState(
                Header: new PackageHeader(Unknown: 0, FileCount: 0, ExtraFileCount: 0),
                Files: [],
                ExtraFiles: [],
                FirstFileInfoOffset: 0);
            return Task.FromResult(new PackageEditor(handle, encryptor, fileTableWriter, headerSerializer, state));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a package file for editing.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="xlGamesKey">The AES key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="PackageEditor"/> with exclusive read/write access to the package.</returns>
    /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static async Task<PackageEditor> OpenAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, CancellationToken cancellationToken = default)
    {
        var handle = File.OpenHandle(packagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous);
        try
        {
            var encryptor = new Encryptor(xlGamesKey.Span);
            var fileTableWriter = new FileTableWriter(encryptor);
            var headerSerializer = new PackageHeaderSerializer(encryptor);
            var state = await FileTableHelper
                .LoadRecordsForEditingAsync(handle, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new PackageEditor(handle, encryptor, fileTableWriter, headerSerializer, state);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    /// <exception cref="IOException">An I/O error occurred while writing to the package file.</exception>
    public async Task AddOrReplaceFileAsync(string name, Stream data,
        PackageWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        var sizeHint = options?.SizeHint;
        var creation = options?.CreationTime;
        var modified = options?.ModifiedTime;

        if (data.CanSeek)
        {
            await AddOrReplaceSeekableAsync(name, data, creation, modified, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (sizeHint.HasValue)
        {
            await AddOrReplaceWithSizeHintAsync(name, data, sizeHint.Value, creation, modified, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await AddOrReplaceNonSeekableAsync(name, data, creation, modified, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <exception cref="IOException">An I/O error occurred while writing to the package file.</exception>
    public async Task AddOrReplaceFileAsync(string name, ReadOnlyMemory<byte> data,
        PackageWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        var placement = DeterminePlacement(name, data.Length);

        await RandomAccess.WriteAsync(_handle, data, placement.Offset, cancellationToken).ConfigureAwait(false);

        Span<byte> md5 = stackalloc byte[16];
        MD5.HashData(data.Span, md5);
        CommitRecord(name, placement, data.Length, md5, options?.CreationTime, options?.ModifiedTime);
    }

    /// <inheritdoc />
    public void DeleteFile(string name)
    {
        var index = FindFileIndex(name);
        if (index < 0)
        {
            throw new FileNotFoundException($"File '{name}' was not found in the package.", name);
        }

        var record = _files[index];

        // Create an __unused__ extra-file record that marks the space as available for reuse.
        var extraRecord = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("__unused__"),
            FileOffset: record.FileOffset,
            FileSize: record.FileSize + record.PaddingSize,
            StoredSize: record.FileSize + record.PaddingSize,
            PaddingSize: 0,
            Md5: default,
            Reserved1: 0,
            CreationTime: default,
            ModifiedTime: default,
            AesPadding: 0);

        _files.RemoveAt(index);
        _entriesList.RemoveAt(index);
        _extraFiles.Add(extraRecord);

        IsDirty = true;
    }

    /// <inheritdoc />
    public void RenameFile(string oldName, string newName)
    {
        var index = FindFileIndex(oldName);
        if (index < 0)
            throw new FileNotFoundException($"File '{oldName}' was not found in the package.", oldName);

        if (FindFileIndex(newName) >= 0)
            throw new InvalidOperationException($"A file named '{newName}' already exists in the package.");

        var newRecord = _files[index] with { FileName = PackedFileRecord.FileNameBuffer.FromString(newName) };
        _files[index] = newRecord;
        _entriesList[index] = new PackageEntry(_handle, newRecord);
        IsDirty = true;
    }

    /// <inheritdoc />
    /// <exception cref="IOException">An I/O error occurred while writing to the package file.</exception>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        const int headerBlockSize = 512;
        const int encryptedHeaderSize = 32;

        // Sort active files by offset so the on-disk table order matches what LoadRecordsAsync returns.
        // We sort a local copy only — mutating _files/_entriesList here would fire spurious Replace
        // events on the PackageTreeView (swapping entries with different names triggers remove+add
        // cycles that desynchronise _fileIndex from the node tree, causing subsequent deletes to
        // silently fail to remove the node).
        // _extraFiles is intentionally excluded — its insertion order must be preserved.
        var sortedFiles = _files.OrderBy(r => r.FileOffset).ToList();

        // Intentionally not disposing the file stream to avoid disposing the package file handle that we don't own
        var stream = new FileStream(_handle, FileAccess.Write, bufferSize: 80 * 1024, isAsync: true);
        stream.Seek(_firstFileInfoOffset, SeekOrigin.Begin);

        await _fileTableWriter.WriteFileRecordsAsync(stream, sortedFiles, cancellationToken).ConfigureAwait(false);
        await _fileTableWriter.WriteFileRecordsAsync(stream, _extraFiles, cancellationToken).ConfigureAwait(false);

        // Pad to the next 512-byte boundary
        var position = stream.Position;
        var dif = position % 512;
        if (dif > 0)
        {
            var padding = new byte[512 - dif];
            await stream.WriteAsync(padding, cancellationToken).ConfigureAwait(false);
        }

        // Write the 512-byte header block: 32 encrypted bytes followed by 480 zero bytes
        Span<byte> encryptedHeader = stackalloc byte[encryptedHeaderSize];
        _headerSerializer.Serialize(_header, _files.Count, _extraFiles.Count, encryptedHeader);
        await stream.WriteAsync(encryptedHeader.ToArray(), cancellationToken).ConfigureAwait(false);

        var remainingHeaderBytes = new byte[headerBlockSize - encryptedHeaderSize];
        await stream.WriteAsync(remainingHeaderBytes, cancellationToken).ConfigureAwait(false);

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Truncate the file to the current position, removing any stale FAT/header bytes
        stream.SetLength(stream.Position);

        IsDirty = false;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _encryptor.Dispose();
        _handle.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────────────────

    private readonly record struct Placement(long Offset, int PaddingSize, int? ExistingFileIndex,
        int? ExtraFileIndex);

    /// <summary>
    /// Determines the best placement for a file of the given size.
    /// The returned placement's offset is where data should be written.
    /// Also advances <see cref="_firstFileInfoOffset"/> if appending.
    /// </summary>
    private Placement DeterminePlacement(string name, long dataLength)
    {
        // Try in-place replace first
        var existingIndex = FindFileIndex(name);
        if (existingIndex >= 0)
        {
            var existing = _files[existingIndex];
            if (dataLength <= existing.FileSize + existing.PaddingSize)
            {
                var paddingSize = (int)(existing.FileSize + existing.PaddingSize - dataLength);
                return new Placement(existing.FileOffset, paddingSize, existingIndex, null);
            }
        }

        int? existingIndexOrNull = existingIndex >= 0 ? existingIndex : null;

        // Try reusing a deleted slot (first-fit)
        for (var i = 0; i < _extraFiles.Count; i++)
        {
            var extra = _extraFiles[i];
            if (extra.FileSize >= dataLength)
            {
                var paddingSize = (int)(extra.FileSize - dataLength);
                return new Placement(extra.FileOffset, paddingSize, existingIndexOrNull, i);
            }
        }

        // Append at the end of the data section
        var appendOffset = _firstFileInfoOffset;
        var appendPadding = (int)((512 - dataLength % 512) % 512);
        _firstFileInfoOffset += dataLength + appendPadding;
        return new Placement(appendOffset, appendPadding, existingIndexOrNull, null);
    }

    /// <summary>
    /// Updates (or creates) the <see cref="PackedFileRecord"/> and <see cref="PackageEntry"/> for the
    /// given file after a successful write.
    /// </summary>
    private void CommitRecord(string name, Placement placement, long dataLength,
        ReadOnlySpan<byte> md5Bytes,
        DateTimeOffset? explicitCreationTime = null, DateTimeOffset? explicitModifiedTime = null)
    {
        var md5 = new PackedFileRecord.Md5Buffer();
        md5Bytes.CopyTo(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref md5, 1)));

        var now = DateTimeOffset.UtcNow;
        var modifiedTime = new PackedFileRecord.WindowsFileTime
        {
            Value = (explicitModifiedTime ?? now).ToFileTime()
        };

        PackedFileRecord newRecord;
        if (placement.ExistingFileIndex.HasValue)
        {
            var existing = _files[placement.ExistingFileIndex.Value];
            // Use caller-supplied creation time if provided; otherwise preserve the original.
            var creationTime = explicitCreationTime.HasValue
                ? new PackedFileRecord.WindowsFileTime { Value = explicitCreationTime.Value.ToFileTime() }
                : existing.CreationTime;
            newRecord = existing with
            {
                FileOffset = placement.Offset,
                FileSize = dataLength,
                StoredSize = dataLength,
                PaddingSize = placement.PaddingSize,
                Md5 = md5,
                CreationTime = creationTime,
                ModifiedTime = modifiedTime
            };
        }
        else
        {
            var creationTime = new PackedFileRecord.WindowsFileTime
            {
                Value = (explicitCreationTime ?? now).ToFileTime()
            };
            newRecord = new PackedFileRecord(
                FileName: PackedFileRecord.FileNameBuffer.FromString(name),
                FileOffset: placement.Offset,
                FileSize: dataLength,
                StoredSize: dataLength,
                PaddingSize: placement.PaddingSize,
                Md5: md5,
                Reserved1: 0,
                CreationTime: creationTime,
                ModifiedTime: modifiedTime,
                AesPadding: 0);
        }

        // Remove the reused extra-file slot if applicable
        if (placement.ExtraFileIndex.HasValue)
        {
            _extraFiles.RemoveAt(placement.ExtraFileIndex.Value);
        }

        var newEntry = new PackageEntry(_handle, newRecord);

        if (placement.ExistingFileIndex.HasValue)
        {
            _files[placement.ExistingFileIndex.Value] = newRecord;
            _entriesList[placement.ExistingFileIndex.Value] = newEntry;
        }
        else
        {
            _files.Add(newRecord);
            _entriesList.Add(newEntry);
        }

        IsDirty = true;
    }

    private async Task AddOrReplaceWithSizeHintAsync(string name, Stream data, long sizeHint,
        DateTimeOffset? creationTime, DateTimeOffset? modifiedTime, CancellationToken cancellationToken)
    {
        // Save _firstFileInfoOffset before DeterminePlacement in case placement chooses append
        // and we need to roll back on error.
        var savedFirstFileInfoOffset = _firstFileInfoOffset;
        var placement = DeterminePlacement(name, sizeHint);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(80 * 1024);
        var buffer = bufferOwner.Memory;

        var writeOffset = placement.Offset;
        long totalWritten = 0;
        int bytesRead;
        while ((bytesRead = await data.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (totalWritten + bytesRead > sizeHint)
            {
                // Restore _firstFileInfoOffset so the next append starts at the correct position.
                _firstFileInfoOffset = savedFirstFileInfoOffset;
                throw new InvalidOperationException(
                    $"Stream data exceeds the provided size hint of {sizeHint} bytes for file '{name}'.");
            }

            var chunk = buffer[..bytesRead];
            hash.AppendData(chunk.Span);
            await RandomAccess.WriteAsync(_handle, chunk, writeOffset, cancellationToken).ConfigureAwait(false);
            writeOffset += bytesRead;
            totalWritten += bytesRead;
        }

        Span<byte> md5 = stackalloc byte[16];
        hash.GetHashAndReset(md5);

        // Commit using actual totalWritten; any shortfall vs sizeHint becomes extra PaddingSize.
        CommitRecord(name, placement, totalWritten, md5, creationTime, modifiedTime);
    }

    private async Task AddOrReplaceSeekableAsync(string name, Stream data,
        DateTimeOffset? creationTime, DateTimeOffset? modifiedTime, CancellationToken cancellationToken)
    {
        var dataLength = data.Length - data.Position;
        var placement = DeterminePlacement(name, dataLength);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(80 * 1024);
        var buffer = bufferOwner.Memory;

        var writeOffset = placement.Offset;
        int bytesRead;
        while ((bytesRead = await data.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            var chunk = buffer[..bytesRead];
            hash.AppendData(chunk.Span);
            await RandomAccess.WriteAsync(_handle, chunk, writeOffset, cancellationToken).ConfigureAwait(false);
            writeOffset += bytesRead;
        }

        Span<byte> md5 = stackalloc byte[16];
        hash.GetHashAndReset(md5);

        CommitRecord(name, placement, dataLength, md5, creationTime, modifiedTime);
    }

    private async Task AddOrReplaceNonSeekableAsync(string name, Stream data,
        DateTimeOffset? creationTime, DateTimeOffset? modifiedTime, CancellationToken cancellationToken)
    {
        // Length unknown: always append. If a file with the same name already exists, delete it
        // first (freeing its space as a reusable slot) before writing the new data.
        var existingIndex = FindFileIndex(name);
        if (existingIndex >= 0)
        {
            DeleteFile(name);
        }

        var appendOffset = _firstFileInfoOffset;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(80 * 1024);
        var buffer = bufferOwner.Memory;

        var writeOffset = appendOffset;
        long totalWritten = 0;
        int bytesRead;
        while ((bytesRead = await data.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            var chunk = buffer[..bytesRead];
            hash.AppendData(chunk.Span);
            await RandomAccess.WriteAsync(_handle, chunk, writeOffset, cancellationToken).ConfigureAwait(false);
            writeOffset += bytesRead;
            totalWritten += bytesRead;
        }

        var paddingSize = (int)((512 - totalWritten % 512) % 512);
        _firstFileInfoOffset = appendOffset + totalWritten + paddingSize;

        Span<byte> md5 = stackalloc byte[16];
        hash.GetHashAndReset(md5);

        var placement = new Placement(appendOffset, paddingSize, null, null);
        CommitRecord(name, placement, totalWritten, md5, creationTime, modifiedTime);
    }

    private int FindFileIndex(string name)
    {
        for (var i = 0; i < _files.Count; i++)
        {
            if (_files[i].FileName.ToString() == name)
            {
                return i;
            }
        }

        return -1;
    }
}