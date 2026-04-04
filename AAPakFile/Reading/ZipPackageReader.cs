using System.Collections.ObjectModel;
using System.IO.Compression;

namespace AAPakFile.Reading;

/// <summary>
/// Provides read-only access to the contents of a ZIP archive via the <see cref="IPackageReader"/>
/// contract.
/// </summary>
/// <remarks>
/// <para>
/// Open a <see cref="ZipPackageReader"/> via <see cref="OpenAsync"/> or
/// <see cref="PackageFile.OpenZipReaderAsync"/>. The ZIP archive is opened only to read the
/// central directory; no file handle is held after <see cref="OpenAsync"/> returns. Consequently
/// <see cref="DisposeAsync"/> is a no-op.
/// </para>
/// <para>
/// <see cref="Entries"/> is populated at open time and does not change for the lifetime of
/// the reader. Directory entries (whose names end with '/') are excluded, consistent with
/// <see cref="AAPakFile.Import.ZipImporter"/>.
/// </para>
/// <para>
/// <see cref="PackageReaderEntry.OpenRead"/> opens the ZIP file independently on each call,
/// so concurrent calls on different entries are safe. Each returned stream is non-seekable and
/// holds its own file handle and <see cref="ZipArchive"/> instance until disposed.
/// </para>
/// <para>
/// <see cref="PackageReaderEntry.Md5Hash"/> returns an empty span for all entries — ZIP archives
/// do not store MD5 hashes.
/// </para>
/// </remarks>
public sealed class ZipPackageReader : IPackageReader
{
    private readonly ReadOnlyCollection<PackageReaderEntry> _entries;

    private ZipPackageReader(IList<PackageReaderEntry> entries)
    {
        _entries = new ReadOnlyCollection<PackageReaderEntry>(entries);
    }

    /// <inheritdoc />
    public IReadOnlyList<PackageReaderEntry> Entries => _entries;

    /// <summary>
    /// Opens a ZIP archive for reading.
    /// </summary>
    /// <param name="zipFilePath">The path to the ZIP archive.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ZipPackageReader"/> with its <see cref="Entries"/> fully populated.
    /// Directory entries are excluded. Dispose the reader when done; it holds no persistent
    /// file handle.
    /// </returns>
    /// <exception cref="IOException">An I/O error occurred while opening the ZIP archive.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access <paramref name="zipFilePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="zipFilePath"/> does not exist.
    /// </exception>
    public static Task<ZipPackageReader> OpenAsync(string zipFilePath,
        CancellationToken cancellationToken = default)
    {
        // ZipArchive reads the central directory synchronously regardless of the stream's async
        // flag, so no true async work occurs here. We return a completed task to keep the public
        // API consistent with PackageReader.OpenAsync.
        var fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 1, useAsync: true);
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }

        List<PackageReaderEntry> entries;
        try
        {
            // ZIP spec encodes directory entries with a trailing '/'; they contain no file data.
            // entry.FullName already uses '/' per the ZIP specification.
            entries = archive.Entries
                .Where(e => !e.FullName.EndsWith('/'))
                .Select(e => CreateEntry(zipFilePath, e))
                .ToList();
        }
        finally
        {
            archive.Dispose();
            fileStream.Dispose();
        }

        return Task.FromResult(new ZipPackageReader(entries));
    }

    private static PackageReaderEntry CreateEntry(string zipFilePath, ZipArchiveEntry zipEntry)
    {
        // Capture by value before the lambda runs; zipEntry is only valid while the archive
        // is open, but the delegate will be called later.
        var fullName = zipEntry.FullName;
        var length = zipEntry.Length;
        var time = zipEntry.LastWriteTime; // ZIP stores a single timestamp; use it for both fields

        return new PackageReaderEntry(
            name: fullName,
            fileSize: length,
            creationTime: time,
            modifiedTime: time,
            md5Hash: [],
            openRead: () => OpenZipEntry(zipFilePath, fullName));
    }

    private static Stream OpenZipEntry(string zipFilePath, string entryFullName)
    {
        // Each OpenRead() call opens its own FileStream + ZipArchive so that concurrent calls
        // on different entries are independently safe (ZipArchive does not support concurrent
        // entry reads on a shared instance).
        //
        // useAsync: false — ZipArchive reads the stream synchronously; FILE_FLAG_OVERLAPPED
        // would force synchronous I/O through the IOCP machinery, which is strictly worse.
        var fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 1, useAsync: false);
        ZipArchive archive;
        try
        {
            // leaveOpen: true — ZipEntryStream owns both fileStream and archive and disposes
            // them in order on its own Dispose.
            archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }

        ZipArchiveEntry? entry;
        try
        {
            entry = archive.GetEntry(entryFullName);
        }
        catch
        {
            archive.Dispose();
            fileStream.Dispose();
            throw;
        }

        if (entry is null)
        {
            archive.Dispose();
            fileStream.Dispose();
            throw new FileNotFoundException(
                $"Entry '{entryFullName}' was not found in the ZIP archive.", entryFullName);
        }

        Stream entryStream;
        try
        {
            entryStream = entry.Open();
        }
        catch
        {
            archive.Dispose();
            fileStream.Dispose();
            throw;
        }

        return new ZipEntryStream(fileStream, archive, entryStream);
    }

    /// <inheritdoc />
    /// <remarks>
    /// This implementation is a no-op. <see cref="ZipPackageReader"/> holds no persistent file
    /// handle; the ZIP archive is opened and closed during <see cref="OpenAsync"/>.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// A read-only stream over a single ZIP archive entry that owns the underlying
    /// <see cref="FileStream"/> and <see cref="ZipArchive"/> and disposes them when closed.
    /// </summary>
    private sealed class ZipEntryStream : Stream
    {
        private readonly FileStream _fileStream;
        private readonly ZipArchive _archive;
        private readonly Stream _entryStream;

        internal ZipEntryStream(FileStream fileStream, ZipArchive archive, Stream entryStream)
        {
            _fileStream = fileStream;
            _archive = archive;
            _entryStream = entryStream;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length
            => throw new NotSupportedException("ZipEntryStream does not support Length.");

        public override long Position
        {
            get => throw new NotSupportedException("ZipEntryStream does not support Position.");
            set => throw new NotSupportedException("ZipEntryStream does not support Position.");
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _entryStream.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer)
            => _entryStream.Read(buffer);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _entryStream.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _entryStream.ReadAsync(buffer, cancellationToken);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException("ZipEntryStream does not support seeking.");

        public override void SetLength(long value)
            => throw new NotSupportedException("ZipEntryStream does not support SetLength.");

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("ZipEntryStream does not support writing.");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _entryStream.Dispose();
                _archive.Dispose();     // disposes the ZipArchive; leaveOpen: true means it
                _fileStream.Dispose();  // does not touch fileStream — we dispose it here
            }
            base.Dispose(disposing);
        }
    }
}