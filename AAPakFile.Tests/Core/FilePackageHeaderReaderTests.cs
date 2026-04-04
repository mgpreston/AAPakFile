using System.Diagnostics.CodeAnalysis;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Core;

public class FilePackageHeaderReaderTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    // Same pattern as StreamPackageHeaderReaderTests.FakeParser.
    private class FakeParser : IPackageHeaderParser
    {
        private readonly List<byte[]> _calls = [];

        public IReadOnlyList<byte[]> Calls => _calls;

        public bool TryParse(Span<byte> data, [NotNullWhen(true)] out PackageHeader? header)
        {
            _calls.Add(data.ToArray());
            header = null;
            return false;
        }

        public PackageHeader Parse(Span<byte> data)
        {
            _calls.Add(data.ToArray());
            return new PackageHeader(0, 0, 0);
        }
    }

    /// <summary>
    /// Fake <see cref="IRandomAccessReader"/> that returns a fixed length and dequeues pre-configured
    /// return values from each <see cref="Read"/> call (without touching a real file handle).
    /// </summary>
    private sealed class FakeRandomAccessReader(long length, params int[] readReturnValues) : IRandomAccessReader
    {
        private readonly Queue<int> _returns = new(readReturnValues);

        public long GetLength(SafeFileHandle handle) => length;

        public int Read(SafeFileHandle handle, Span<byte> buffer, long fileOffset)
        {
            var count = _returns.TryDequeue(out var n) ? n : 0;
            // Fill buffer[..count] with deterministic bytes so assertions can inspect the data.
            for (var i = 0; i < count; i++)
                buffer[i] = (byte)((fileOffset + i) % 251);
            return count;
        }
    }

    // A dummy SafeFileHandle that is never dereferenced (used with FakeRandomAccessReader).
    private static SafeFileHandle DummyHandle() => new(nint.Zero, ownsHandle: false);

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(511)]
    public void ReadHeader_InsufficientLength_ThrowsInvalidDataException(int fileSize)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[fileSize]);

            var parser = new FakeParser();
            var cut = new FilePackageHeaderReader(parser);

            using var handle = File.OpenHandle(path);

            // ReSharper disable once AccessToDisposedClosure
            Assert.Throws<InvalidDataException>(() => cut.ReadHeader(handle));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [Arguments(512)]
    [Arguments(1024)]
    [Arguments(1537)]
    public async Task ReadHeader_ReadsFirst32BytesOfLastBlock(int fileSize, CancellationToken cancellationToken)
    {
        const int blockSize = 512;
        const int headerSize = 32;

        var path = Path.GetTempFileName();
        try
        {
            var content = new byte[fileSize];
            for (var i = 0; i < fileSize; i++)
            {
                content[i] = (byte)(i % 251); // use a prime to get varied values
            }

            await File.WriteAllBytesAsync(path, content, cancellationToken);

            var parser = new FakeParser();
            var cut = new FilePackageHeaderReader(parser);

            using var handle = File.OpenHandle(path);
            cut.ReadHeader(handle);

            var call = await Assert.That(parser.Calls).HasSingleItem();

            // The header block is at fileSize - 512; the first 32 bytes of it are passed to the parser.
            var expected = content.AsSpan(fileSize - blockSize, headerSize).ToArray();
            await Assert.That(call.SequenceEqual(expected)).IsTrue();
        }
        finally { File.Delete(path); }
    }

    // ── IRandomAccessReader injection ──────────────────────────────────────────

    [Test]
    public void ReadHeader_ReadReturnsZero_ThrowsInvalidDataException()
    {
        // File length passes the guard (>= 512), but the first Read returns 0 — simulates
        // a TOCTOU truncation between GetLength and Read.
        var fakeReader = new FakeRandomAccessReader(length: 512, readReturnValues: [0]);
        var cut = new FilePackageHeaderReader(new FakeParser(), fakeReader);

        Assert.Throws<InvalidDataException>(() => cut.ReadHeader(DummyHandle()));
    }

    [Test]
    public async Task ReadHeader_PartialRead_LoopsUntilBufferFull()
    {
        // First Read returns 16 bytes, second returns the remaining 16 — exercises the loop back-edge.
        var fakeReader = new FakeRandomAccessReader(length: 512, readReturnValues: [16, 16]);
        var parser = new FakeParser();
        var cut = new FilePackageHeaderReader(parser, fakeReader);

        cut.ReadHeader(DummyHandle());

        // Parser should have been called exactly once with a complete 32-byte buffer.
        var call = await Assert.That(parser.Calls).HasSingleItem();
        await Assert.That(call.Length).IsEqualTo(32);
    }
}