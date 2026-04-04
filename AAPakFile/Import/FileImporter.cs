using AAPakFile.Editing;

namespace AAPakFile.Import;

/// <summary>
/// Imports files from a directory in the file system into a package.
/// </summary>
public class FileImporter : IFileImporter
{
    /// <inheritdoc />
    public async Task ImportAsync(IPackageEditor editor, string sourceFolder,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories);

        // Try to get the count without enumerating. Directory.EnumerateFiles returns a lazy
        // FileSystemEnumerable<string> which does not implement ICollection<T>, so this will
        // return false and total stays 0. TryGetNonEnumeratedCount is used as a forward-compatible
        // idiom: if a future BCL version makes EnumerateFiles count-capable, it will start working
        // automatically.
        int? totalFilesCount = files.TryGetNonEnumeratedCount(out var totalFiles) ? totalFiles : null;

        // Defer Entries notifications for the duration of the import so subscribers receive
        // a single batched replay rather than one event per file.
        using var deferral = editor.DeferNotifications();

        var imported = 0;
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Normalise path separators: Path.GetRelativePath returns OS-native separators (\\ on
            // Windows), but the package stores names with forward slashes for consistency.
            var relativePath = Path.GetRelativePath(sourceFolder, filePath).Replace('\\', '/');

            // bufferSize: 1 disables FileStream's own internal buffer. The editor already rents an
            // 80 KB pool buffer internally, so a second layer of buffering is wasteful.
            // The stream is seekable, so the editor routes to AddOrReplaceSeekableAsync and uses
            // DeterminePlacement for in-place replace, slot reuse, or append as appropriate.
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 1, useAsync: true);

            var fileInfo = new FileInfo(filePath);
            var options = new PackageWriteOptions
            {
                CreationTime = new DateTimeOffset(fileInfo.CreationTimeUtc, TimeSpan.Zero),
                ModifiedTime = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)
            };

            await editor.AddOrReplaceFileAsync(relativePath, stream, options, cancellationToken)
                .ConfigureAwait(false);

            imported++;
            progress?.Report(new ImportProgress(imported, TotalFilesCount: totalFilesCount));
        }
    }
}