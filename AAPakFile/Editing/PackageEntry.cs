using AAPakFile.Core;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Editing;

/// <summary>
/// A read-only view of a single file stored inside an open <see cref="PackageEditor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Entry instances are live views: if the underlying record is replaced by
/// <see cref="IPackageEditor.AddOrReplaceFileAsync(string, System.IO.Stream, AAPakFile.Editing.PackageWriteOptions, System.Threading.CancellationToken)"/>,
/// the old entry object is removed from <see cref="IPackageEditor.Entries"/> and a new one is added.
/// </para>
/// </remarks>
public sealed class PackageEntry
{
    private readonly SafeFileHandle _packageHandle;
    private readonly PackedFileRecord _record;

    internal PackageEntry(SafeFileHandle packageHandle, PackedFileRecord record)
    {
        _packageHandle = packageHandle;
        _record = record;
    }

    /// <summary>
    /// Gets the name of the file, including any path information.
    /// </summary>
    public string Name => _record.FileName;

    /// <summary>
    /// Gets the size of the file's content in bytes.
    /// </summary>
    public long FileSize => _record.FileSize;

    /// <summary>
    /// Gets the date and time at which the file was created.
    /// </summary>
    public DateTimeOffset CreationTime => _record.CreationTime.AsDateTimeOffset();

    /// <summary>
    /// Gets the date and time at which the file was last modified.
    /// </summary>
    public DateTimeOffset ModifiedTime => _record.ModifiedTime.AsDateTimeOffset();

    /// <summary>
    /// Opens a read-only stream over this entry's data inside the package.
    /// </summary>
    /// <returns>
    /// A <see cref="Stream"/> positioned at the start of the entry's data. The caller is responsible for
    /// disposing the stream.
    /// </returns>
    public Stream OpenRead() =>
        new PackedFileStream(_packageHandle, _record.FileOffset, _record.FileSize);
}