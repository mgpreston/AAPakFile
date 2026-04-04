using System.Collections.ObjectModel;

using AAPakFile.Core;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Reading;

/// <summary>
/// Provides read-only access to the contents of a package file.
/// </summary>
/// <remarks>
/// <para>
/// Open a <see cref="PackageReader"/> via <see cref="OpenAsync"/> or
/// <see cref="PackageFile.OpenReaderAsync"/>. The file is opened with shared read access
/// (<see cref="FileShare.Read"/>), so multiple readers — and other read-only handles — may
/// be open on the same file simultaneously.
/// </para>
/// <para>
/// <see cref="Entries"/> is populated at open time and does not change for the lifetime of
/// the reader. Opening a reader concurrently with a <see cref="AAPakFile.Editing.PackageEditor"/>
/// on the same file will fail, because the editor requires exclusive access.
/// </para>
/// <para>
/// <see cref="PackageReaderEntry.OpenRead"/> is thread-safe: multiple callers may open and
/// read streams from different entries simultaneously.
/// </para>
/// </remarks>
public sealed class PackageReader : IPackageReader
{
    private readonly SafeFileHandle _handle;
    private readonly ReadOnlyCollection<PackageReaderEntry> _entries;

    private PackageReader(SafeFileHandle handle, IList<PackageReaderEntry> entries)
    {
        _handle = handle;
        _entries = new ReadOnlyCollection<PackageReaderEntry>(entries);
    }

    /// <inheritdoc />
    public IReadOnlyList<PackageReaderEntry> Entries => _entries;

    /// <summary>
    /// Opens a package file for reading with shared read access.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="xlGamesKey">
    /// The AES decryption key for the package. If empty, the default XLGames key is used.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="PackageReader"/> with its <see cref="Entries"/> fully populated.
    /// Dispose the reader to release the file handle.
    /// </returns>
    /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static async Task<PackageReader> OpenAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, CancellationToken cancellationToken = default)
    {
        var handle = File.OpenHandle(packagePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, FileOptions.Asynchronous);
        try
        {
            var records = await FileTableHelper
                .LoadRecordsAsync(handle, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var entries = records
                .Select(r => new PackageReaderEntry(handle, r))
                .ToList();

            return new PackageReader(handle, entries);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _handle.Dispose();
        return ValueTask.CompletedTask;
    }
}