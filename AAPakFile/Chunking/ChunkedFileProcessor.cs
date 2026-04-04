using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Chunking;

/// <summary>
/// A class that allows for the processing of a package's file contents in contiguous chunks of a defined size.
/// </summary>
internal class ChunkedFileProcessor : IChunkedFileProcessor
{
    private sealed record Context<TState>(
        IChunkedFileProcessor.ProcessChunkDelegate<TState> ProcessChunkDelegate,
        IProgress<IChunkedFileProcessor.Progress>? ProgressReporter);

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public async Task<TState> ProcessAsync<TState>(SafeFileHandle packageHandle,
        IEnumerable<PackedFileRecord> fileRecords, TState state,
        IChunkedFileProcessor.ProcessChunkDelegate<TState> processChunkDelegate,
        IProgress<IChunkedFileProcessor.Progress>? progressReporter, CancellationToken cancellationToken)
    {
        var context = new Context<TState>(processChunkDelegate, progressReporter);

        // TODO: Make the sizes configurable
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 10 * 1024 * 1024,
            resumeWriterThreshold: 5 * 1024 * 1024));

        // Split the package contents into large chunks for efficient reading.
        var chunks = Chunker.SplitIntoChunks(fileRecords);

        var readFromPackageIntoPipeTask = ReadFromPackage(pipe.Writer, chunks, packageHandle, cancellationToken);
        var processChunksTask = ProcessChunksAsync(pipe.Reader, fileRecords, state, context, cancellationToken);

        await Task.WhenAll(readFromPackageIntoPipeTask, processChunksTask).ConfigureAwait(false);

        // Both tasks succeeded; return the consumer's result.
        return processChunksTask.Result;
    }

    private static async Task ReadFromPackage(PipeWriter pipeWriter, IEnumerable<Chunker.ReadChunk> chunks,
        SafeFileHandle packageHandle, CancellationToken cancellationToken)
    {
        const int maxPipeBlockSize = 5 * 1024 * 1024; //128 * 1024;

        try
        {
            foreach (var chunk in chunks)
            {
                var remaining = chunk.Length;
                var position = chunk.StartOffset;

                while (remaining > 0)
                {
                    // Get a writable region from the pipe
                    var sizeToRead = Math.Min(remaining, maxPipeBlockSize);
                    var memory = pipeWriter.GetMemory(sizeToRead)[..sizeToRead];

                    // Read directly into the pipe's memory
                    var bytesRead = await RandomAccess.ReadAsync(packageHandle, memory, position, cancellationToken)
                        .ConfigureAwait(false);

                    if (bytesRead == 0)
                        throw new EndOfStreamException($"Unexpected EOF at offset {position}.");

                    pipeWriter.Advance(bytesRead);
                    position += bytesRead;
                    remaining -= bytesRead;
                }

                // Only flush once for each chunk. If the reads are small, this is more efficient than flushing after each read.
                var flushResult = await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCompleted)
                    break;
            }

            await pipeWriter.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await pipeWriter.CompleteAsync(ex).ConfigureAwait(false);
            throw;
        }
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    private static async Task<TState> ProcessChunksAsync<TState>(PipeReader reader,
        IEnumerable<PackedFileRecord> fileRecords, TState state, Context<TState> context,
        CancellationToken cancellationToken)
    {
        try
        {
            var processedFileCounter = 0;
            var totalFileCount = fileRecords.Count();

            // Enumerate all file records in offset order (records are expected to be pre-sorted by FileOffset,
            // matching the order in which the pipe was written to).
            foreach (var fileRecord in fileRecords)
            {
                var remaining = fileRecord.StoredSize;
                var currentOffsetInFile = 0L;

                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    var buffer = result.Buffer;

                    if (buffer.Length == 0 && result.IsCompleted)
                        throw new EndOfStreamException("Unexpected EOF while reading chunk");

                    var fileLengthInBuffer = Math.Min(remaining, result.Buffer.Length);
                    var fileBuffer = buffer.Slice(0, fileLengthInBuffer);

                    var isLastPartOfFile = currentOffsetInFile + fileBuffer.Length >= fileRecord.StoredSize;
                    state = await context.ProcessChunkDelegate(fileRecord, fileBuffer, currentOffsetInFile,
                        isLastPartOfFile,
                        state, cancellationToken).ConfigureAwait(false);
                    currentOffsetInFile += fileBuffer.Length;
                    remaining -= fileBuffer.Length;

                    reader.AdvanceTo(buffer.GetPosition(fileLengthInBuffer));
                }

                processedFileCounter++;
                context.ProgressReporter?.Report(
                    new IChunkedFileProcessor.Progress(processedFileCounter, totalFileCount));
            }

            await reader.CompleteAsync().ConfigureAwait(false);

            return state;
        }
        catch (Exception ex)
        {
            await reader.CompleteAsync(ex).ConfigureAwait(false);
            throw;
        }
    }
}