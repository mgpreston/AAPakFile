using System.Buffers;

using AAPakFile.Chunking;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Export;

/// <summary>
/// An abstract class that provides a base for derived classes that export packed file contents from a package.
/// </summary>
/// <typeparam name="TState">The type of state to pass between calls during processing.</typeparam>
public abstract class ExporterBase<TState>(IChunkedFileProcessor chunkedFileProcessor)
{
    /// <summary>
    /// Starts the export process and returns when all files have been exported.
    /// </summary>
    /// <param name="packageHandle">A handle to the package file.</param>
    /// <param name="fileRecords">The packed files to export.</param>
    /// <param name="state">The user-defined state object.</param>
    /// <param name="progressReporter">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{TResult}"/> that contains the user-provided state object.</returns>
    protected virtual async Task<TState> ExportAsync(SafeFileHandle packageHandle,
        IEnumerable<PackedFileRecord> fileRecords, TState state, IProgress<ExportProgress>? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        IProgress<IChunkedFileProcessor.Progress>? progressAdapter = progressReporter is null ? null :
            new SyncProgress<IChunkedFileProcessor.Progress>(progress =>
                progressReporter.Report(new ExportProgress(progress.ProcessedFilesCount, progress.TotalFilesCount)));
        return await chunkedFileProcessor.ProcessAsync(packageHandle, fileRecords, state, ProcessChunkAsync,
            progressAdapter, cancellationToken);
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    /// <summary>
    /// When overridden in a derived class, processes a chunk of a packed file.
    /// </summary>
    /// <param name="fileRecord">The file record currently being processed.</param>
    /// <param name="buffer">The raw bytes of the file in the chunk being processed.</param>
    /// <param name="currentOffsetInFile">The offset within the packed file where <paramref name="buffer"/> starts.</param>
    /// <param name="isBufferLast">Whether this is the last buffer belonging to the file.</param>
    /// <param name="state">The user-provided state object.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that contains the user-provided state object to pass to the next
    /// <see cref="ProcessChunkAsync"/> call.
    /// </returns>
    protected abstract ValueTask<TState> ProcessChunkAsync(PackedFileRecord fileRecord, ReadOnlySequence<byte> buffer,
        long currentOffsetInFile, bool isBufferLast, TState state, CancellationToken cancellationToken);
}