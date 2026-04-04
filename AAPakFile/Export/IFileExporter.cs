using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Export;

/// <summary>
/// Defines an interface for exporting packed files to a directory in the file system.
/// </summary>
public interface IFileExporter
{
    /// <summary>
    /// Exports packed files to the specified output folder asynchronously.
    /// </summary>
    /// <param name="packageHandle">The handle to the package being processed.</param>
    /// <param name="fileRecords">The collection of file records to export.</param>
    /// <param name="outputFolder">The path to the output folder where files will be exported.</param>
    /// <param name="progressReporter">An optional progress reporter to track export progress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    Task ExportAsync(SafeFileHandle packageHandle, IEnumerable<PackedFileRecord> fileRecords,
        string outputFolder, IProgress<ExportProgress>? progressReporter = null,
        CancellationToken cancellationToken = default);
}