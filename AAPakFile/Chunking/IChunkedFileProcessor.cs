using System.Buffers;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Chunking;

/// <summary>
/// Defines an interface for the processing of a package's file contents in contiguous chunks of a defined size.
/// </summary>
public interface IChunkedFileProcessor
{
    /// <summary>
    /// Represents the progress of processing packed file chunks.
    /// </summary>
    /// <param name="ProcessedFilesCount">The number of files that have been processed.</param>
    /// <param name="TotalFilesCount">The total number of files to process.</param>
    public readonly record struct Progress(int ProcessedFilesCount, int TotalFilesCount);

    /// <summary>
    /// Processes a chunk of a packed file's content.
    /// </summary>
    /// <param name="fileRecord">The record of the packed file being processed.</param>
    /// <param name="buffer">A sequence of bytes that represent a chunk of the packed file.</param>
    /// <param name="currentOffsetInFile">
    /// The offset within the packed file where <paramref name="buffer"/> begins.
    /// </param>
    /// <param name="isBufferLast">
    /// A value indicating whether <paramref name="buffer"/> is the last chunk of the packed file.
    /// </param>
    /// <param name="state">The user-defined state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <typeparam name="TState">The type of the user-defined state.</typeparam>
    /// <returns>A <see cref="ValueTask{TResult}"/> whose result is the user-defined state.</returns>
    public delegate ValueTask<TState> ProcessChunkDelegate<TState>(PackedFileRecord fileRecord,
        ReadOnlySequence<byte> buffer, long currentOffsetInFile, bool isBufferLast, TState state,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously processes the contents of the specified file records in contiguous chunks.
    /// </summary>
    /// <param name="packageHandle">The handle to the package file.</param>
    /// <param name="fileRecords">
    /// The file records within the package to process, sorted ascending by
    /// <see cref="PackedFileRecord.FileOffset"/>.
    /// </param>
    /// <param name="state">The user-defined state to pass to the processing function.</param>
    /// <param name="processChunkDelegate">
    /// A function that receives a chunk of data belonging to a single file for processing.
    /// </param>
    /// <param name="progressReporter">An object to receive progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <typeparam name="TState">The type of the user-defined state passed to the processing function.</typeparam>
    /// <returns>
    /// A <see cref="Task{T}"/> that returns the user-defined state from the final processing function call.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The processing delegate is invoked once for each chunk. Each chunk contains either the full contents of a packed
    /// file (if the file is small enough to fit into one chunk), or the partial contents of a packed file.
    /// In the case of processing only the partial contents, the next invocation of the processing delegate is
    /// guaranteed to contain the next logical part of the same file.
    /// </para>
    /// <para>
    /// The <paramref name="state"/> value is passed into the first invocation of the processing delegate. The return
    /// value from the processing delegate is then passed into the following invocation. The result of the final
    /// invocation is returned from this method.
    /// </para>
    /// </remarks>
    Task<TState> ProcessAsync<TState>(SafeFileHandle packageHandle, IEnumerable<PackedFileRecord> fileRecords,
        TState state, ProcessChunkDelegate<TState> processChunkDelegate, IProgress<Progress>? progressReporter,
        CancellationToken cancellationToken);
}