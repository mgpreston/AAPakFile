namespace AAPakFile.Chunking;

public class ChunkerTests
{
    [Test]
    public async Task SplitIntoChunks_WithNoRecords_ReturnsEmpty()
    {
        var result = Chunker.SplitIntoChunks([]);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    [Arguments(0, 1, 1)]
    [Arguments(1, 1, 1)]
    [Arguments(50, 1, 1)]
    [Arguments(0, 100, 100)]
    [Arguments(0, 100, 101)]
    [Arguments(100, 10, 100)]
    public async Task SplitIntoChunks_WithSingleRecord_ReturnsExactChunk(int offset, int size, int maxChunkSize)
    {
        var record = new PackedFileRecord
        {
            FileOffset = offset,
            StoredSize = size,
            FileSize = size
        };
        var result = Chunker.SplitIntoChunks([record], maxChunkSize);

        var single = await Assert.That(result).HasSingleItem();
        await Assert.That(single.StartOffset).IsEqualTo(record.FileOffset);
        await Assert.That(single.Length).IsEqualTo(checked((int)record.StoredSize));
    }

    [Test]
    [Arguments(0, 1, 1, 2)]
    [Arguments(1, 1, 1, 2)]
    [Arguments(50, 1, 1, 2)]
    [Arguments(0, 100, 50, 150)]
    [Arguments(0, 100, 50, 151)]
    [Arguments(100, 10, 20, 100)]
    public async Task SplitIntoChunks_WithTwoContiguousRecords_SmallerThanChunkSize_ReturnsSingleChunk(int offset, int size1,
        int size2, int maxChunkSize)
    {
        var record1 = new PackedFileRecord
        {
            FileOffset = offset,
            StoredSize = size1,
            FileSize = size1
        };
        var record2 = new PackedFileRecord
        {
            FileOffset = offset + size1, // contiguous, start at the end of the first file
            StoredSize = size2,
            FileSize = size2
        };
        var result = Chunker.SplitIntoChunks([record1, record2], maxChunkSize);

        var single = await Assert.That(result).HasSingleItem();
        await Assert.That(single.StartOffset).IsEqualTo(record1.FileOffset);
        await Assert.That(single.Length).IsEqualTo(checked((int)(record1.StoredSize + record2.StoredSize)));
    }

    [Test]
    [Arguments(0, 1, 2, 1, 5)]
    [Arguments(1, 1, 3, 1, 5)]
    [Arguments(50, 1, 52, 1, 5)]
    [Arguments(0, 100, 150, 50, 200)]
    [Arguments(0, 100, 150, 50, 201)]
    [Arguments(100, 10, 115, 20, 50)]
    public async Task SplitIntoChunks_WithTwoNonContiguousRecords_SmallerThanChunkSize_ReturnsTwoChunks(int offset1,
        int size1, int offset2, int size2, int maxChunkSize)
    {
        var record1 = new PackedFileRecord
        {
            FileOffset = offset1,
            StoredSize = size1,
            FileSize = size1
        };
        var record2 = new PackedFileRecord
        {
            FileOffset = offset2,
            StoredSize = size2,
            FileSize = size2
        };
        var result = Chunker.SplitIntoChunks([record1, record2], maxChunkSize).ToList();

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].StartOffset).IsEqualTo(record1.FileOffset);
        await Assert.That(result[0].Length).IsEqualTo(checked((int)record1.StoredSize));
        await Assert.That(result[1].StartOffset).IsEqualTo(record2.FileOffset);
        await Assert.That(result[1].Length).IsEqualTo(checked((int)record2.StoredSize));
    }

    [Test]
    [Arguments(0, 100, 50)]
    [Arguments(1, 100, 50)]
    [Arguments(50, 100, 50)]
    [Arguments(0, 200, 10)]
    [Arguments(0, 200, 101)]
    [Arguments(100, 15, 10)]
    public async Task SplitIntoChunks_WithSingleRecord_LargerThanChunkSize_ReturnsMultipleChunks(int offset, int size,
        int maxChunkSize)
    {
        var record = new PackedFileRecord
        {
            FileOffset = offset,
            StoredSize = size,
            FileSize = size
        };
        var result = Chunker.SplitIntoChunks([record], maxChunkSize);

        var expectedNumberOfChunks = (int)Math.Ceiling((float)size / maxChunkSize);

        await Assert.That(result).Count().IsEqualTo(expectedNumberOfChunks);
    }

    /// <summary>
    /// Two contiguous files whose combined size exactly equals <paramref name="maxChunkSize"/>.
    /// This exercises the branch where <c>chunkFull == true</c> AND <c>fileDone == true</c> at
    /// the same time (the second file is completed exactly when the chunk is also full).
    /// </summary>
    [Test]
    [Arguments(0, 50, 50, 100)]
    [Arguments(10, 30, 70, 100)]
    public async Task SplitIntoChunks_TwoContiguousFiles_ThatExactlyFillOneChunk_YieldsOneChunk(
        int offset, int size1, int size2, int maxChunkSize)
    {
        var record1 = new PackedFileRecord
        {
            FileOffset = offset,
            StoredSize = size1,
            FileSize = size1
        };
        var record2 = new PackedFileRecord
        {
            FileOffset = offset + size1,
            StoredSize = size2,
            FileSize = size2
        };

        var result = Chunker.SplitIntoChunks([record1, record2], maxChunkSize).ToList();

        // The two files together fill exactly one chunk; no additional chunks should be emitted.
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].StartOffset).IsEqualTo(offset);
        await Assert.That(result[0].Length).IsEqualTo(maxChunkSize);
    }
}