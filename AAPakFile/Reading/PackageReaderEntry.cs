using AAPakFile.Core;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Reading;

/// <summary>
/// A read-only snapshot of a single file stored inside an open <see cref="PackageReader"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="AAPakFile.Editing.PackageEntry"/>, this type is a fixed snapshot of the
/// record that was loaded when the reader was opened. The data does not change for the lifetime
/// of the <see cref="PackageReader"/>.
/// </remarks>
public sealed class PackageReaderEntry
{
    private readonly byte[] _md5Hash;
    private readonly Func<Stream> _openRead;

    internal PackageReaderEntry(SafeFileHandle packageHandle, PackedFileRecord record)
        : this(
            name: record.FileName,
            fileSize: record.FileSize,
            creationTime: record.CreationTime.AsDateTimeOffset(),
            modifiedTime: record.ModifiedTime.AsDateTimeOffset(),
            md5Hash: record.Md5.AsSpan().ToArray(),
            openRead: () => new PackedFileStream(packageHandle, record.FileOffset, record.FileSize))
    {
    }

    internal PackageReaderEntry(string name, long fileSize, DateTimeOffset creationTime,
        DateTimeOffset modifiedTime, byte[] md5Hash, Func<Stream> openRead)
    {
        Name = name;
        FileSize = fileSize;
        CreationTime = creationTime;
        ModifiedTime = modifiedTime;
        _md5Hash = md5Hash;
        _openRead = openRead;
    }

    /// <summary>
    /// Gets the name of the file, including any path information.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the size of the file's content in bytes.
    /// </summary>
    public long FileSize { get; }

    /// <summary>
    /// Gets the date and time at which the file was created.
    /// </summary>
    public DateTimeOffset CreationTime { get; }

    /// <summary>
    /// Gets the date and time at which the file was last modified.
    /// </summary>
    public DateTimeOffset ModifiedTime { get; }

    /// <summary>
    /// Gets the MD5 hash of the file's content as stored in the package.
    /// </summary>
    /// <remarks>
    /// The returned span is a zero-copy view over data stored in this entry and is valid
    /// for the entry's lifetime. To obtain a hex string, use
    /// <see cref="Convert.ToHexString(ReadOnlySpan{byte})"/>.
    /// Returns an empty span for ZIP-backed entries, which do not store MD5 hashes.
    /// </remarks>
    public ReadOnlySpan<byte> Md5Hash => _md5Hash;

    /// <summary>
    /// Opens a read-only stream over this entry's data.
    /// </summary>
    /// <returns>
    /// A <see cref="Stream"/> positioned at the start of the entry's data. The caller is
    /// responsible for disposing the stream.
    /// </returns>
    /// <remarks>
    /// For PAK-backed entries the returned stream is seekable and multiple callers may call
    /// this method concurrently. For ZIP-backed entries the returned stream is non-seekable;
    /// each call opens the ZIP file independently, so concurrent calls are safe but each
    /// stream holds its own file handle.
    /// </remarks>
    public Stream OpenRead() => _openRead();
}