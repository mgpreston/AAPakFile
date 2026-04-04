using System.IO.Compression;

using AAPakFile.Editing;

namespace AAPakFile.Import;

/// <summary>
/// Imports files from a ZIP archive into a package.
/// </summary>
public class ZipImporter : IZipImporter
{
    /// <inheritdoc />
    public async Task ImportAsync(IPackageEditor editor, string zipFilePath,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // bufferSize: 1 disables FileStream's own internal buffer. ZipArchive manages its own
        // buffering internally when reading compressed data.
        await using var zipStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 1, useAsync: true);
        await using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

        // ZIP spec encodes directory entries with a trailing '/'; they contain no file data.
        // ToList() materialises the filtered collection so we have an accurate count for progress.
        var entries = zipArchive.Entries
            .Where(e => !e.FullName.EndsWith('/'))
            .ToList();

        var total = entries.Count;
        var imported = 0;

        // Defer Entries notifications for the duration of the import so subscribers receive
        // a single batched replay rather than one event per file.
        using var _ = editor.DeferNotifications();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // entry.Open() returns a non-seekable DeflateStream. SizeHint supplies the uncompressed
            // size from the ZIP central directory so the editor can choose the best placement strategy
            // (in-place replace, slot reuse, or append) before reading begins.
            // entry.FullName already uses '/' per the ZIP specification.
            // The ZIP format stores a single "last modified" timestamp; we use it for both fields.
            var options = new PackageWriteOptions
            {
                SizeHint = entry.Length,
                CreationTime = entry.LastWriteTime,
                ModifiedTime = entry.LastWriteTime
            };
            await using var entryStream = entry.Open();
            await editor.AddOrReplaceFileAsync(entry.FullName, entryStream, options, cancellationToken)
                .ConfigureAwait(false);

            imported++;
            progress?.Report(new ImportProgress(imported, TotalFilesCount: total));
        }
    }
}