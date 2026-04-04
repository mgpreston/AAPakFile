using System.Collections.ObjectModel;

namespace AAPakFile.Editing;

/// <summary>
/// Defines an interface for editing the contents of a package file.
/// </summary>
public interface IPackageEditor : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the package has unsaved changes.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Gets a live, observable, read-only view of the current active files in the package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This collection is updated immediately as files are added, replaced, or deleted, and
    /// fires <see cref="System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged"/>
    /// for each targeted change. Subscribers (such as
    /// <see cref="AAPakFile.Tree.PackageTreeView{TEntry}"/>) can react surgically without
    /// polling or diffing.
    /// </para>
    /// <para>
    /// When performing bulk operations, wrap them in a <see cref="DeferNotifications"/> scope
    /// to batch the notifications into a single replay on dispose.
    /// </para>
    /// </remarks>
    ReadOnlyObservableCollection<PackageEntry> Entries { get; }

    /// <summary>
    /// Suppresses <see cref="Entries"/> change notifications until the returned scope is disposed.
    /// </summary>
    /// <returns>
    /// An <see cref="IDisposable"/> that, when disposed, replays all buffered add / remove /
    /// replace events in the order they occurred. Deferral scopes are ref-counted — nested
    /// calls are safe and events are replayed only when the outermost scope is disposed.
    /// If no mutations occurred inside the scope, no event is fired.
    /// </returns>
    /// <remarks>
    /// Built-in bulk operations (such as
    /// <see cref="AAPakFile.Import.IFileImporter.ImportAsync"/> and
    /// <see cref="AAPakFile.Import.IZipImporter.ImportAsync"/>) call
    /// <see cref="DeferNotifications"/> internally. Call it explicitly when performing
    /// multiple add / replace / delete operations that should appear as a single batch to
    /// subscribers:
    /// <code>
    /// using (editor.DeferNotifications())
    /// {
    ///     foreach (var file in files)
    ///         await editor.AddOrReplaceFileAsync(file.Name, file.Stream);
    /// }
    /// </code>
    /// </remarks>
    IDisposable DeferNotifications();

    /// <summary>
    /// Asynchronously adds a new file to the package, or replaces an existing file with the same name.
    /// The file data is read from the provided stream.
    /// </summary>
    /// <param name="name">The name of the file to add or replace, including any path information.</param>
    /// <param name="data">A stream containing the file data to write.</param>
    /// <param name="options">
    /// Optional settings controlling timestamps and placement. When <see langword="null"/>, defaults are
    /// applied: <see cref="DateTimeOffset.UtcNow"/> for both timestamps (preserving the original creation
    /// time when replacing), and automatic placement selection based on stream seekability.
    /// Set <see cref="PackageWriteOptions.SizeHint"/> to enable optimal placement for non-seekable streams.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// <para>
    /// File data is written directly to the package file. Call <see cref="SaveAsync"/> promptly after
    /// completing all edits to ensure the file table is updated and the package remains in a consistent state.
    /// </para>
    /// <para>
    /// If <paramref name="data"/> is seekable, the best available placement strategy (in-place replace,
    /// slot reuse, or append) is applied automatically using the stream's length.
    /// If <paramref name="data"/> is not seekable and <see cref="PackageWriteOptions.SizeHint"/> is set,
    /// placement is chosen from that hint and the write is aborted if the stream exceeds it.
    /// If <paramref name="data"/> is not seekable and no hint is provided, the file is always appended.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="data"/> is not seekable, <see cref="PackageWriteOptions.SizeHint"/> is set,
    /// and the stream yields more bytes than the hint.
    /// </exception>
    /// <exception cref="IOException">An I/O error occurred while writing to the package file.</exception>
    Task AddOrReplaceFileAsync(string name, Stream data,
        PackageWriteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds a new file to the package, or replaces an existing file with the same name.
    /// The file data is provided as an in-memory buffer.
    /// </summary>
    /// <param name="name">The name of the file to add or replace, including any path information.</param>
    /// <param name="data">The file data to write.</param>
    /// <param name="options">
    /// Optional settings controlling timestamps. When <see langword="null"/>, <see cref="DateTimeOffset.UtcNow"/>
    /// is used for both timestamps (preserving the original creation time when replacing).
    /// <see cref="PackageWriteOptions.SizeHint"/> is ignored for in-memory data.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// File data is written directly to the package file. Call <see cref="SaveAsync"/> promptly after
    /// completing all edits to ensure the file table is updated and the package remains in a consistent state.
    /// </remarks>
    /// <exception cref="IOException">An I/O error occurred while writing to the package file.</exception>
    Task AddOrReplaceFileAsync(string name, ReadOnlyMemory<byte> data,
        PackageWriteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the specified file as deleted. The file's on-disk space is retained as reusable free space
    /// for future additions.
    /// </summary>
    /// <param name="name">The name of the file to delete, including any path information.</param>
    /// <exception cref="FileNotFoundException">No file with the specified name exists in the package.</exception>
    void DeleteFile(string name);

    /// <summary>
    /// Renames a file inside the package. The file's content, timestamps, and MD5 hash are unchanged.
    /// Call <see cref="SaveAsync"/> to persist the change.
    /// </summary>
    /// <param name="oldName">The current name of the file, including any path information.</param>
    /// <param name="newName">The new name for the file, including any path information.</param>
    /// <exception cref="FileNotFoundException">No file named <paramref name="oldName"/> exists in the package.</exception>
    /// <exception cref="InvalidOperationException">A file named <paramref name="newName"/> already exists in the package.</exception>
    void RenameFile(string oldName, string newName);

    /// <summary>
    /// Asynchronously persists all pending changes to the package file.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Writes the updated file table and header to disk, then truncates the file to the correct length.
    /// After this method completes, <see cref="IsDirty"/> is <see langword="false"/>.
    /// </remarks>
    /// <exception cref="IOException">An I/O error occurred while writing to the package file.</exception>
    Task SaveAsync(CancellationToken cancellationToken = default);
}