using System.IO.Compression;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Export;

/// <summary>
/// Defines an interface for exporting files into a ZIP archive.
/// </summary>
public interface IZipExporter
{
    /// <summary>
    /// Exports files into a ZIP archive asynchronously.
    /// </summary>
    /// <param name="packageHandle">The handle to the package being processed.</param>
    /// <param name="fileRecords">The collection of file records to export.</param>
    /// <param name="outputFilePath">The path to the output ZIP file.</param>
    /// <param name="progressReporter">An optional progress reporter to track export progress.</param>
    /// <param name="compressionLevel">The compression level for the ZIP archive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    Task ExportAsync(SafeFileHandle packageHandle,
        IEnumerable<PackedFileRecord> fileRecords,
        string outputFilePath,
        IProgress<ExportProgress>? progressReporter = null,
        CompressionLevel compressionLevel = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default);
}