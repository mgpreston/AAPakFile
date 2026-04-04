namespace AAPakFile.Chunking;

/// <summary>
/// Provides logic for splitting a package's file data section into chunks of a defined maximum size.
/// </summary>
internal static class Chunker
{
    /// <summary>
    /// Represents a chunk of a package's file data section, potentially containing the contents of more than one stored
    /// file.
    /// </summary>
    /// <param name="StartOffset">The offset within the package where the chunk starts.</param>
    /// <param name="Length">The length of the chunk, in bytes.</param>
    public sealed record ReadChunk(long StartOffset, int Length);

    /// <summary>
    /// Splits the file contents of the specified records into chunks of a defined maximum size.
    /// </summary>
    /// <param name="records">The file records to chunk.</param>
    /// <param name="maxChunkSize">The maximum size of each chunk, in bytes.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> whose elements represent chunks of the package file.</returns>
    /// <remarks>
    /// <para>
    /// This method is designed to split a package file into large chunks for efficient reading, such as when exporting
    /// the file contents.
    /// </para>
    /// <para>
    /// Each chunk can be up to <paramref name="maxChunkSize"/> bytes in size. If a single file is larger than the
    /// maximum chunk size, its contents will be split across more than one chunk.
    /// Multiple files that are contiguous (beginning where the previous file ended) can be stored into the same chunk.
    /// If files are non-contiguous, they will be split into separate chunks, potentially smaller than the maximum chunk
    /// size.
    /// </para>
    /// <para>
    /// <paramref name="records"/> must be sorted in ascending order by <see cref="PackedFileRecord.FileOffset"/>.
    /// </para>
    /// </remarks>
    public static IEnumerable<ReadChunk> SplitIntoChunks(
        IEnumerable<PackedFileRecord> records,
        int maxChunkSize = 5 * 1024 * 1024)
    {
        // Records are expected to be pre-sorted ascending by FileOffset (see FileTableHelper.LoadRecordsAsync).
        using var enumerator = records.GetEnumerator();
        if (!enumerator.MoveNext())
            yield break;

        var currentChunkStart = enumerator.Current.FileOffset;
        var currentChunkLength = 0;
        var currentFilePos = enumerator.Current.FileOffset;
        var currentFileRemaining = enumerator.Current.StoredSize;

        while (true)
        {
            var availableInFile = currentFileRemaining;
            var availableInChunk = maxChunkSize - currentChunkLength;
            var toTake = (int)Math.Min(availableInFile, availableInChunk);

            currentChunkLength += toTake;
            currentFilePos += toTake;
            currentFileRemaining -= toTake;

            var chunkFull = currentChunkLength >= maxChunkSize;
            var fileDone = currentFileRemaining == 0;

            if (chunkFull)
            {
                yield return new ReadChunk(currentChunkStart, currentChunkLength);

                currentChunkStart += currentChunkLength;
                currentChunkLength = 0;
            }

            if (!fileDone)
            {
                continue;
            }

            if (!enumerator.MoveNext())
                break;

            var nextRecord = enumerator.Current;
            var contiguous = nextRecord.FileOffset == currentFilePos;

            // If gap between files, yield remaining chunk
            if (!contiguous && currentChunkLength > 0)
            {
                yield return new ReadChunk(currentChunkStart, currentChunkLength);
                currentChunkStart = nextRecord.FileOffset;
                currentChunkLength = 0;
            }

            currentFilePos = nextRecord.FileOffset;
            currentFileRemaining = nextRecord.StoredSize;
        }

        if (currentChunkLength > 0)
            yield return new ReadChunk(currentChunkStart, currentChunkLength);
    }
}