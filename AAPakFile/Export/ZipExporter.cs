using System.Buffers;
using System.IO.Compression;

using AAPakFile.Chunking;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Export;

/// <summary>
/// A class responsible for exporting files into a ZIP archive.
/// </summary>
/// <remarks>
/// This class processes chunked files and writes them into a ZIP archive using the specified compression level.
/// </remarks>
public class ZipExporter(IChunkedFileProcessor chunkedFileProcessor)
    : ExporterBase<ZipExporter.State>(chunkedFileProcessor), IZipExporter
{
    /// <summary>
    /// Represents the state of the ZIP export process.
    /// </summary>
    /// <param name="ZipArchive">The ZIP archive being written to.</param>
    /// <param name="CompressionLevel">The compression level used for the ZIP archive.</param>
    public sealed record State(ZipArchive ZipArchive, CompressionLevel CompressionLevel) : IAsyncDisposable
    {
        /// <summary>
        /// Gets or sets the current ZIP archive file entry.
        /// </summary>
        public ZipArchiveEntry? CurrentFileEntry { get; set; }

        /// <summary>
        /// Gets or sets the stream of the current ZIP archive file entry.
        /// </summary>
        public Stream? CurrentFileEntryStream { get; set; }

        /// <summary>
        /// Disposes of the current file entry stream asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            if (CurrentFileEntryStream is { } currentFileEntryStream)
            {
                await currentFileEntryStream.DisposeAsync().ConfigureAwait(false);
                CurrentFileEntryStream = null;
                CurrentFileEntry = null;
            }
        }
    }

    /// <summary>
    /// Exports files into a ZIP archive asynchronously.
    /// </summary>
    /// <param name="packageHandle">The handle to the package being processed.</param>
    /// <param name="fileRecords">The collection of file records to export.</param>
    /// <param name="outputFilePath">The path to the output ZIP file.</param>
    /// <param name="progressReporter">An optional progress reporter.</param>
    /// <param name="compressionLevel">The compression level for the ZIP archive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public async Task ExportAsync(SafeFileHandle packageHandle,
        IEnumerable<PackedFileRecord> fileRecords,
        string outputFilePath,
        IProgress<ExportProgress>? progressReporter = null,
        CompressionLevel compressionLevel = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        await using var zipStream = new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.Write);
        await using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

        await using var state = new State(zipArchive, compressionLevel);
        await ExportAsync(packageHandle, fileRecords, state, progressReporter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes a chunk of data and writes it to the ZIP archive.
    /// </summary>
    /// <param name="fileRecord">The file record associated with the chunk.</param>
    /// <param name="buffer">The buffer containing the chunk data.</param>
    /// <param name="currentOffsetInFile">The current offset in the file being processed.</param>
    /// <param name="isBufferLast">Indicates whether this is the last buffer for the file.</param>
    /// <param name="state">The current state of the ZIP export process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated state of the ZIP export process.</returns>
    protected override async ValueTask<State> ProcessChunkAsync(PackedFileRecord fileRecord,
        ReadOnlySequence<byte> buffer, long currentOffsetInFile, bool isBufferLast, State state,
        CancellationToken cancellationToken)
    {
        try
        {
            if (state.CurrentFileEntry is null)
            {
                state.CurrentFileEntry = state.ZipArchive.CreateEntry(fileRecord.FileName, state.CompressionLevel);

                try
                {
                    state.CurrentFileEntry.LastWriteTime = fileRecord.ModifiedTime;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Suppress. Not important if setting the modified time fails.
                }

                state.CurrentFileEntryStream =
                    await state.CurrentFileEntry.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var memory in buffer)
            {
                await state.CurrentFileEntryStream!.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
            }

            return state;
        }
        finally
        {
            if (isBufferLast)
            {
                if (state.CurrentFileEntryStream is not null)
                {
                    await state.CurrentFileEntryStream.DisposeAsync().ConfigureAwait(false);
                    state.CurrentFileEntryStream = null;
                }

                state.CurrentFileEntry = null;
            }
        }
    }
}