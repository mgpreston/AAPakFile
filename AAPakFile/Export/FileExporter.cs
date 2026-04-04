using System.Buffers;

using AAPakFile.Chunking;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Export;

/// <summary>
/// An exporter that exports packed files to a directory in the file system.
/// </summary>
public class FileExporter(IChunkedFileProcessor chunkedFileProcessor)
    : ExporterBase<FileExporter.State>(chunkedFileProcessor), IFileExporter
{
    /// <summary>
    /// Represents the state of the file export process.
    /// </summary>
    /// <param name="OutputFolder">The output folder where files will be exported.</param>
    public sealed record State(string OutputFolder) : IDisposable
    {
        /// <summary>
        /// Gets or sets the handle to the current file being written.
        /// </summary>
        public SafeFileHandle? CurrentFileHandle { get; set; }

        /// <summary>
        /// Disposes of the current file handle.
        /// </summary>
        public void Dispose()
        {
            CurrentFileHandle?.Dispose();
            CurrentFileHandle = null;
        }
    }

    /// <summary>
    /// Exports packed files to the specified output folder asynchronously.
    /// </summary>
    /// <param name="packageHandle">The handle to the package being processed.</param>
    /// <param name="fileRecords">The collection of file records to export.</param>
    /// <param name="outputFolder">The path to the output folder.</param>
    /// <param name="progressReporter">An optional progress reporter to track export progress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public async Task ExportAsync(SafeFileHandle packageHandle, IEnumerable<PackedFileRecord> fileRecords,
        string outputFolder, IProgress<ExportProgress>? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        using var state = new State(outputFolder);
        await ExportAsync(packageHandle, fileRecords, state, progressReporter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes a chunk of data and writes it to the appropriate file in the output folder.
    /// </summary>
    /// <param name="fileRecord">The file record associated with the chunk.</param>
    /// <param name="buffer">The buffer containing the chunk data.</param>
    /// <param name="currentOffsetInFile">The current offset in the file being processed.</param>
    /// <param name="isBufferLast">Indicates whether this is the last buffer for the file.</param>
    /// <param name="state">The current state of the file export process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated state of the file export process.</returns>
    protected override async ValueTask<State> ProcessChunkAsync(PackedFileRecord fileRecord,
        ReadOnlySequence<byte> buffer, long currentOffsetInFile, bool isBufferLast, State state,
        CancellationToken cancellationToken)
    {
        try
        {
            // If we haven't yet opened the file to write to, do it now and keep it open until it's written completely.
            if (state.CurrentFileHandle is null)
            {
                var filePath = Path.Combine(state.OutputFolder, fileRecord.FileName);
                if (Path.GetDirectoryName(filePath) is { } directoryName)
                {
                    Directory.CreateDirectory(directoryName);
                }

                state.CurrentFileHandle = File.OpenHandle(filePath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.Read, FileOptions.Asynchronous, preallocationSize: fileRecord.FileSize);
            }

            foreach (var memory in buffer)
            {
                await RandomAccess.WriteAsync(state.CurrentFileHandle, memory, currentOffsetInFile, cancellationToken)
                    .ConfigureAwait(false);
                currentOffsetInFile += memory.Length;
            }

            return state;
        }
        finally
        {
            if (isBufferLast)
            {
                state.CurrentFileHandle?.Dispose();
                state.CurrentFileHandle = null;
            }
        }
    }
}