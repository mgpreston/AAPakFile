using System.IO.Compression;

using AAPakFile.Core;

namespace AAPakFile.Export;

/// <summary>
/// Extension methods for types derived from <see cref="ExporterBase{TState}"/>.
/// </summary>
public static class ExporterExtensions
{
    /// <summary>
    /// Exports all files from a package to a specified folder asynchronously.
    /// </summary>
    /// <param name="exporter">The <see cref="FileExporter"/> instance used for exporting files.</param>
    /// <param name="packagePath">The path to the package file to be exported.</param>
    /// <param name="outputPath">The path to the output folder where files will be exported.</param>
    /// <param name="xlGamesKey">An optional decryption key for the package.</param>
    /// <param name="filter">An optional predicate to select which files to export. If <see langword="null"/>, all files are exported.</param>
    /// <param name="progress">An optional progress reporter to track export progress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public static async Task ExportAllToFolderAsync(this FileExporter exporter, string packagePath, string outputPath,
        ReadOnlyMemory<byte> xlGamesKey = default, Func<PackedFileRecord, bool>? filter = null,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var packageHandle = File.OpenHandle(packagePath, options: FileOptions.Asynchronous);
        IEnumerable<PackedFileRecord> fileRecords = await FileTableHelper
            .LoadRecordsAsync(packageHandle, xlGamesKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (filter is not null) fileRecords = fileRecords.Where(filter);
        await exporter.ExportAsync(packageHandle, fileRecords, outputPath, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Exports all files from a package to a ZIP archive asynchronously.
    /// </summary>
    /// <param name="exporter">The <see cref="ZipExporter"/> instance used for exporting files.</param>
    /// <param name="packagePath">The path to the package file to be exported.</param>
    /// <param name="zipFilePath">The path to the output ZIP file where files will be exported.</param>
    /// <param name="xlGamesKey">An optional decryption key for the package.</param>
    /// <param name="filter">An optional predicate to select which files to export. If <see langword="null"/>, all files are exported.</param>
    /// <param name="compressionLevel">The compression level to use for the ZIP archive.</param>
    /// <param name="progress">An optional progress reporter to track export progress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public static async Task ExportAllToZipArchiveAsync(this ZipExporter exporter, string packagePath,
        string zipFilePath, ReadOnlyMemory<byte> xlGamesKey = default, Func<PackedFileRecord, bool>? filter = null,
        CompressionLevel compressionLevel = CompressionLevel.Optimal, IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var packageHandle = File.OpenHandle(packagePath, options: FileOptions.Asynchronous);
        IEnumerable<PackedFileRecord> fileRecords = await FileTableHelper
            .LoadRecordsAsync(packageHandle, xlGamesKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (filter is not null) fileRecords = fileRecords.Where(filter);
        await exporter.ExportAsync(packageHandle, fileRecords, zipFilePath, progress, compressionLevel,
            cancellationToken).ConfigureAwait(false);
    }
}