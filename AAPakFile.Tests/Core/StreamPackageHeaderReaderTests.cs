using System.Diagnostics.CodeAnalysis;

namespace AAPakFile.Core;

public class StreamPackageHeaderReaderTests
{
    // Due to the use of ref structs (Span<T>), we can't mock using Moq, so use a test fake instead.
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

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(511)]
    public void ReadHeader_InsufficientLength_ThrowsException(int length)
    {
        // Due to alignment, the header occupies a 512 byte block at the end of the file.
        // There must be at least 512 bytes to read the header from.

        var cut = new StreamPackageHeaderReader(new FakeParser());

        var buffer = new byte[length];
        using var stream = new MemoryStream(buffer);

        // ReSharper disable once AccessToDisposedClosure
        Assert.Throws<InvalidDataException>(() => cut.ReadHeader(stream));
    }

    [Test]
    [Arguments(512)]
    [Arguments(513)]
    [Arguments(1510)]
    public async Task ReadHeader_ReadsFromCorrectLocation(int size)
    {
        // Due to alignment, the header occupies the first 32 bytes of a 512 byte block at the end of the file
        const int blockSize = 512;
        const int headerSize = 32;

        var parserFake = new FakeParser();
        var cut = new StreamPackageHeaderReader(parserFake);

        var buffer = new byte[size];
        for (var i = 0; i < size; i++)
        {
            buffer[i] = (byte)(i % byte.MaxValue);
        }

        using var stream = new MemoryStream(buffer);

        cut.ReadHeader(stream);

        var start = size - blockSize;
        var expectedBlock = buffer.AsSpan(start, headerSize).ToArray();

        var call = await Assert.That(parserFake.Calls).HasSingleItem();
        await Assert.That(call).IsSequenceEqualTo(expectedBlock);
    }
}