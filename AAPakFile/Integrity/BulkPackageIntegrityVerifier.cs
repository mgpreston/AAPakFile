using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Channels;

using AAPakFile.Chunking;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Integrity;

/// <summary>
/// This class provides methods for verifying the integrity of multiple packed files.
/// </summary>
/// <remarks>
/// This class is optimized for multiple packed files.
/// For single packed files, consider using <see cref="PackageIntegrityVerifier"/>.
/// </remarks>
/// <seealso cref="PackageIntegrityVerifier"/>
public class BulkPackageIntegrityVerifier(IChunkedFileProcessor chunkedFileProcessor) : IBulkPackageIntegrityVerifier
{
    private sealed class VerifyState(
        IncrementalHash incrementalHash,
        ChannelWriter<BulkPackedFileIntegrityResult> resultWriter,
        int totalFiles)
    {
        public IncrementalHash IncrementalHash { get; } = incrementalHash;
        public ChannelWriter<BulkPackedFileIntegrityResult> ResultWriter { get; } = resultWriter;
        public int TotalFiles { get; } = totalFiles;
        public int ProcessedFiles { get; set; }
        public int IntactFiles { get; set; }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<BulkPackedFileIntegrityResult> VerifyAsync(SafeFileHandle packageHandle,
        IEnumerable<PackedFileRecord> fileRecords, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<BulkPackedFileIntegrityResult>();

        // Create a linked CTS so we can cancel our background task when the reader stops
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = VerifyCoreAsync(chunkedFileProcessor, packageHandle, fileRecords, channel.Writer, linkedCts.Token);

        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
        return ReadWithCancellationAsync(channel.Reader, linkedCts, cancellationToken);
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    private static async Task VerifyCoreAsync(IChunkedFileProcessor chunkedFileProcessor, SafeFileHandle packageHandle,
        IEnumerable<PackedFileRecord> fileRecords, ChannelWriter<BulkPackedFileIntegrityResult> channelWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            var totalFiles = fileRecords.Count();
            var state = new VerifyState(
                IncrementalHash.CreateHash(HashAlgorithmName.MD5),
                channelWriter,
                totalFiles: totalFiles);
            _ = await chunkedFileProcessor
                .ProcessAsync(packageHandle, fileRecords, state, ProcessChunkAsync, null, cancellationToken)
                .ConfigureAwait(false);
            channelWriter.Complete();
        }
        catch (Exception ex)
        {
            // This method is not awaited, so we ensure no exceptions escape it.
            channelWriter.Complete(ex);
        }
    }

    private static ValueTask<VerifyState> ProcessChunkAsync(PackedFileRecord fileRecord, ReadOnlySequence<byte> buffer,
        long currentOffsetInFile, bool isBufferLast, VerifyState state, CancellationToken cancellationToken)
    {
        foreach (var memory in buffer)
        {
            state.IncrementalHash.AppendData(memory.Span);
        }

        if (isBufferLast)
        {
            Span<byte> calculatedHash = stackalloc byte[state.IncrementalHash.HashLengthInBytes];
            state.IncrementalHash.GetHashAndReset(calculatedHash);

            var isHashCorrect = calculatedHash.SequenceEqual(fileRecord.Md5.AsSpan());

            state.ProcessedFiles += 1;
            state.IntactFiles += isHashCorrect ? 1 : 0;

            var written = state.ResultWriter.TryWrite(new BulkPackedFileIntegrityResult(fileRecord, isHashCorrect,
                state.ProcessedFiles, state.IntactFiles, state.TotalFiles));
            Debug.Assert(written);
        }

        return ValueTask.FromResult(state);
    }

    private static async IAsyncEnumerable<BulkPackedFileIntegrityResult> ReadWithCancellationAsync(
        ChannelReader<BulkPackedFileIntegrityResult> reader,
        CancellationTokenSource cts,
        [EnumeratorCancellation] CancellationToken externalCancellation = default)
    {
        using var _ = cts; // ensure CTS is disposed

        try
        {
            await foreach (var result in reader.ReadAllAsync(externalCancellation))
            {
                yield return result;
            }
        }
        finally
        {
            // This is called when enumeration stops, for ANY reason:
            //   - caller breaks early
            //   - caller disposes enumerator
            //   - caller's cancellation is triggered
            //   - exception thrown
            await cts.CancelAsync();
        }
    }
}