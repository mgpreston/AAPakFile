using System.Buffers;
using System.Collections.ObjectModel;

using AAPakFile.Core;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Reading;

/// <summary>
/// Opens an ArcheRage <c>game_pak</c> file for reading.
/// </summary>
/// <remarks>
/// <para>
/// A <c>game_pak</c> file is a flat concatenation of 100,000+ independent CryTek
/// sub-archives. Two archive types are present:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>flags=0xFFFF0000</c> — model-manifest archives (~3 per file). Contain mesh,
///     LOD, bounding-sphere, and developer-path entries. Their plaintext entry data is
///     fully accessible; a separate AES-encrypted region referenced via an internal
///     sub-index is not exposed by this reader.
///   </description></item>
///   <item><description>
///     <c>flags=0xFFFF0001</c> — game-asset archives (~132,000+ per file). Fully
///     plaintext; contain property blocks, descriptors, and variable-size data.
///   </description></item>
/// </list>
/// <para>
/// All offsets stored in each sub-archive's entry table are relative to the start of that
/// sub-archive. This reader converts them to absolute file offsets before exposing them via
/// <see cref="PackageReaderEntry.OpenRead"/>.
/// </para>
/// <para>
/// Entry names follow the pattern <c>{archiveIndex:D6}_{entryIndex:D4}_attr{attr:D2}</c>,
/// for example <c>"000000_0000_attr19"</c>.
/// </para>
/// <para>
/// <see cref="PackageReaderEntry.Md5Hash"/> returns an empty span for all entries —
/// the format does not store MD5 hashes per entry.
/// <see cref="PackageReaderEntry.CreationTime"/> and
/// <see cref="PackageReaderEntry.ModifiedTime"/> return <see cref="DateTimeOffset.MinValue"/>.
/// </para>
/// </remarks>
internal sealed class GamePakReader : IPackageReader
{
    private readonly SafeFileHandle _handle;
    private readonly ReadOnlyCollection<PackageReaderEntry> _entries;

    /// <inheritdoc/>
    public IReadOnlyList<PackageReaderEntry> Entries => _entries;

    private GamePakReader(SafeFileHandle handle, List<PackageReaderEntry> entries)
    {
        _handle = handle;
        _entries = new ReadOnlyCollection<PackageReaderEntry>(entries);
    }

    /// <summary>
    /// Opens the specified <c>game_pak</c> file and scans all sub-archives, returning a
    /// reader with its <see cref="Entries"/> fully populated.
    /// </summary>
    /// <param name="path">Path to the <c>game_pak</c> file.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="GamePakReader"/> with all sub-archive entries loaded.
    /// Dispose the reader to release the underlying file handle.
    /// </returns>
    /// <exception cref="FormatException">
    /// The file does not begin with a valid CryTek archive header.
    /// </exception>
    internal static async Task<GamePakReader> OpenAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, FileOptions.Asynchronous);
        try
        {
            var entries = await ScanArchivesAsync(handle, cancellationToken)
                .ConfigureAwait(false);
            return new GamePakReader(handle, entries);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    // Scans the file archive-by-archive.
    // After reading each archive's table the reader jumps to that archive's computed data
    // end, then searches forward in expanding windows for the next "CryTek" magic.
    // This avoids a full linear scan of the file while still handling:
    //   - tightly-packed 0xFFFF0001 archives (≤ 512-byte gaps)
    //   - large encrypted gaps between 0xFFFF0000 archives (~32 MB each)
    private static async Task<List<PackageReaderEntry>> ScanArchivesAsync(
        SafeFileHandle handle, CancellationToken ct)
    {
        var fileLength = RandomAccess.GetLength(handle);
        var entries = new List<PackageReaderEntry>();
        long searchPos = 0;
        int archiveIndex = 0;

        while (searchPos < fileLength)
        {
            var archiveBase = await FindNextArchiveAsync(handle, searchPos, fileLength, ct)
                .ConfigureAwait(false);
            if (archiveBase < 0)
                break;

            var dataEnd = await ReadArchiveAsync(handle, archiveBase, archiveIndex, entries, ct)
                .ConfigureAwait(false);

            searchPos = archiveBase + dataEnd + 1;
            archiveIndex++;
        }

        return entries;
    }

    // Scans forward from `fromPos` for a valid CryTek header using expanding windows:
    // 512 B → 4 KB → 64 KB → 1 MB, then a full streaming scan.
    // Returns the absolute file offset of the header, or -1 if none is found.
    private static async Task<long> FindNextArchiveAsync(
        SafeFileHandle handle, long fromPos, long fileLength, CancellationToken ct)
    {
        int[] windowSizes = [512, 4096, 65536, 1 << 20];
        foreach (var windowSize in windowSizes)
        {
            var available = (int)Math.Min(windowSize, fileLength - fromPos);
            if (available < 24)
                break;

            using var owner = MemoryPool<byte>.Shared.Rent(available);
            var buf = owner.Memory[..available];
            await RandomAccess.ReadAsync(handle, buf, fromPos, ct).ConfigureAwait(false);

            var span = buf.Span;
            for (var i = 0; i <= span.Length - 24; i++)
            {
                if (!span[i..].StartsWith(GamePakFormat.Magic))
                    continue;
                try
                {
                    GamePakFormat.ParseHeader(span[i..(i + 24)]);
                    return fromPos + i;
                }
                catch (FormatException)
                {
                    // False positive — byte sequence happens to start with "CryTek"
                    // but the rest of the header is invalid. Keep scanning.
                }
            }

            // Advance with a 5-byte overlap so we never miss a magic split across windows.
            fromPos += Math.Max(1, available - 5);
            if (fromPos >= fileLength)
                break;
        }

        // Fall through to a full streaming scan for the remaining file.
        return await ScanRemainingAsync(handle, fromPos, fileLength, ct).ConfigureAwait(false);
    }

    private static async Task<long> ScanRemainingAsync(
        SafeFileHandle handle, long pos, long fileLength, CancellationToken ct)
    {
        using var owner = MemoryPool<byte>.Shared.Rent(65536);
        const int overlap = 5;

        while (pos < fileLength)
        {
            var n = (int)Math.Min(65536, fileLength - pos);
            await RandomAccess.ReadAsync(handle, owner.Memory[..n], pos, ct).ConfigureAwait(false);

            var span = owner.Memory.Span[..n];
            for (var i = 0; i <= span.Length - 24; i++)
            {
                if (!span[i..].StartsWith(GamePakFormat.Magic))
                    continue;
                try
                {
                    GamePakFormat.ParseHeader(span[i..(i + 24)]);
                    return pos + i;
                }
                catch (FormatException) { }
            }

            pos += Math.Max(1, n - overlap);
        }

        return -1;
    }

    // Reads one archive at `archiveBase`, appends its entries to `entries`.
    // Returns the furthest byte offset used by any entry, relative to archiveBase.
    private static async Task<long> ReadArchiveAsync(
        SafeFileHandle handle, long archiveBase, int archiveIndex,
        List<PackageReaderEntry> entries, CancellationToken ct)
    {
        // Header (24 bytes)
        var headerBuf = new byte[24];
        await RandomAccess.ReadAsync(handle, headerBuf, archiveBase, ct).ConfigureAwait(false);
        (_, uint count) = GamePakFormat.ParseHeader(headerBuf);

        // Entry table (count × 20 bytes)
        var tableBytes = (int)(count * GamePakFormat.EntryBytes);
        var tableBuf = new byte[tableBytes];
        await RandomAccess.ReadAsync(handle, tableBuf, archiveBase + 24, ct).ConfigureAwait(false);

        // Parse all entries upfront so we can compute data sizes via offset-difference.
        var parsed = new (ushort Attr, uint Field4, uint RelOff, uint FieldA, uint SizeField)[(int)count];
        for (var i = 0; i < (int)count; i++)
            parsed[i] = GamePakFormat.ParseEntry(tableBuf, i);

        long dataEnd = 0;
        for (var i = 0; i < (int)count; i++)
        {
            var (attr, _, relOff, _, sizeField) = parsed[i];

            // Prefer offset-difference for data size; fall back to the SizeField for the
            // last entry in the archive (where there is no next offset to diff against).
            uint dataSize = i + 1 < (int)count
                ? parsed[i + 1].RelOff - relOff
                : sizeField;

            if (dataSize == 0)
                continue;

            var absoluteOffset = archiveBase + relOff;
            var capturedOffset = absoluteOffset;
            var capturedSize = (long)dataSize;

            entries.Add(new PackageReaderEntry(
                name: $"{archiveIndex:D6}_{i:D4}_attr{attr:D2}",
                fileSize: capturedSize,
                creationTime: DateTimeOffset.MinValue,
                modifiedTime: DateTimeOffset.MinValue,
                md5Hash: [],
                openRead: () => new PackedFileStream(handle, capturedOffset, capturedSize)));

            var entryEnd = (long)relOff + dataSize;
            if (entryEnd > dataEnd)
                dataEnd = entryEnd;
        }

        return dataEnd;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _handle.Dispose();
        return ValueTask.CompletedTask;
    }
}