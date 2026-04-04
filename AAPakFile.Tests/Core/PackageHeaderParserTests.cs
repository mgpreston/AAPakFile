using System.Buffers.Binary;

namespace AAPakFile.Core;

public class PackageHeaderParserTests
{
    // Due to the use of ref structs (ReadOnlySpan<T>), we can't mock using Moq, so use a test fake instead.
    private class FakeDecryptor(Memory<byte> result) : IDecryptor
    {
        private readonly List<(byte[] CipherText, byte[] Destination)> _calls = [];

        public IReadOnlyList<(byte[] CipherText, byte[] Destination)> Calls => _calls;

        public void Decrypt(ReadOnlySpan<byte> ciphertext, Span<byte> destination)
        {
            _calls.Add((ciphertext.ToArray(), destination.ToArray()));
            result.Span.CopyTo(destination);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(31)]
    [Arguments(33)]
    [Arguments(50)]
    public void Parse_WithIncorrectLength_ThrowsArgumentException(int length)
    {
        var cut = new PackageHeaderParser(new FakeDecryptor(new byte[32]));
        var buffer = new byte[length];

        Assert.Throws<ArgumentException>(() => cut.Parse(buffer));
    }

    [Test]
    public async Task Parse_DecryptsGivenData()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, 0, 0, 0);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        _ = cut.Parse(buffer);

        var (cipherText, destination) = await Assert.That(decryptorFake.Calls).HasSingleItem();
        await Assert.That(cipherText).IsSequenceEqualTo(buffer);
        await Assert.That(destination.Length >= 32).IsTrue();
    }

    [Test]
    public void Parse_ThrowsOnInvalidMagic()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: false, 0, 0, 0);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        Assert.Throws<FormatException>(() => cut.Parse(buffer));
    }

    [Test]
    public void Parse_ThrowsOnTooManyFiles()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, 0, (uint)int.MaxValue + 1, 0);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        Assert.Throws<FormatException>(() => cut.Parse(buffer));
    }

    [Test]
    public void Parse_ThrowsOnTooManyExtraFiles()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, 0, 0, (uint)int.MaxValue + 1);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        Assert.Throws<FormatException>(() => cut.Parse(buffer));
    }

    [Test]
    [Arguments(int.MaxValue, (uint)int.MaxValue, (uint)int.MaxValue)]
    [Arguments(int.MinValue, 0, 0)]
    public async Task Parse_HappyPath(int unknown, uint files, uint extraFiles)
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, unknown, files, extraFiles);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        var header = cut.Parse(buffer);

        await Assert.That(header.Unknown).IsEqualTo(unknown);
        await Assert.That(header.FileCount).IsEqualTo((int)files);
        await Assert.That(header.ExtraFileCount).IsEqualTo((int)extraFiles);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(31)]
    [Arguments(33)]
    [Arguments(50)]
    public async Task TryParse_WithIncorrectLength_ThrowsArgumentException(int length)
    {
        var cut = new PackageHeaderParser(new FakeDecryptor(new byte[32]));
        var buffer = new byte[length];

        var result = cut.TryParse(buffer, out var header);

        await Assert.That(result).IsFalse();
        await Assert.That(header).IsNull();
    }

    [Test]
    public async Task TryParse_DecryptsGivenData()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, 0, 0, 0);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        _ = cut.TryParse(buffer, out _);

        (byte[] cipherText, byte[] destination) = await Assert.That(decryptorFake.Calls).HasSingleItem();
        await Assert.That(cipherText).IsSequenceEqualTo(buffer);
        await Assert.That(destination.Length >= 32).IsTrue();
    }

    [Test]
    public async Task TryParse_ReturnsFalseOnInvalidMagic()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: false, 0, 0, 0);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        var result = cut.TryParse(buffer, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParse_ReturnsFalseOnTooManyFiles()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, 0, (uint)int.MaxValue + 1, 0);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        var result = cut.TryParse(buffer, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParse_RetursnFalseOnTooManyExtraFiles()
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, 0, 0, (uint)int.MaxValue + 1);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        var result = cut.TryParse(buffer, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    [Arguments(int.MaxValue, (uint)int.MaxValue, (uint)int.MaxValue)]
    [Arguments(int.MinValue, 0, 0)]
    public async Task TryParse_HappyPath(int unknown, uint files, uint extraFiles)
    {
        var fakeDecryptedData = CreateHeaderData(withValidMagic: true, unknown, files, extraFiles);
        var decryptorFake = new FakeDecryptor(fakeDecryptedData);

        var cut = new PackageHeaderParser(decryptorFake);
        var buffer = new byte[32];

        var result = cut.TryParse(buffer, out var header);

        await Assert.That(result).IsTrue();
        await Assert.That(header).IsNotNull();
        await Assert.That(header.Unknown).IsEqualTo(unknown);
        await Assert.That(header.FileCount).IsEqualTo((int)files);
        await Assert.That(header.ExtraFileCount).IsEqualTo((int)extraFiles);
    }

    private static byte[] CreateHeaderData(bool withValidMagic, int unknown, uint files, uint extraFiles)
    {
        var data = new byte[32];
        if (withValidMagic)
        {
            PackageHeader.HeaderMagic.Span.CopyTo(data);
        }

        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), unknown);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), files);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12), extraFiles);
        return data;
    }
}