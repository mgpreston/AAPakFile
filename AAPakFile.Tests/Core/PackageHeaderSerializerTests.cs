using System.Buffers.Binary;

namespace AAPakFile.Core;

public class PackageHeaderSerializerTests
{
    private class FakeEncryptor : IEncryptor
    {
        public byte[]? LastPlaintext { get; private set; }
        public byte[]? LastDestination { get; private set; }

        public void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> destination)
        {
            LastPlaintext = plaintext.ToArray();
            LastDestination = destination.ToArray();
            // Identity: copy plaintext to destination.
            plaintext.CopyTo(destination);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(31)]
    [Arguments(33)]
    public void Serialize_WrongDestinationSize_ThrowsArgumentException(int size)
    {
        var fake = new FakeEncryptor();
        var cut = new PackageHeaderSerializer(fake);
        var header = new PackageHeader(Unknown: 0, FileCount: 0, ExtraFileCount: 0);
        var destination = new byte[size];

        Assert.Throws<ArgumentException>(() => cut.Serialize(header, 0, 0, destination));
    }

    [Test]
    public async Task Serialize_PlaintextHasCorrectMagicAtOffset0()
    {
        var fake = new FakeEncryptor();
        var cut = new PackageHeaderSerializer(fake);
        var header = new PackageHeader(Unknown: 0, FileCount: 0, ExtraFileCount: 0);
        var destination = new byte[32];

        cut.Serialize(header, 0, 0, destination);

        await Assert.That(fake.LastPlaintext).IsNotNull();
        var magic = fake.LastPlaintext!.AsSpan(0, 4).ToArray();
        await Assert.That(magic).IsSequenceEqualTo(PackageHeader.HeaderMagic.ToArray());
    }

    [Test]
    [Arguments(42)]
    [Arguments(int.MinValue)]
    [Arguments(int.MaxValue)]
    public async Task Serialize_PlaintextHasCorrectUnknownAtOffset4(int unknown)
    {
        var fake = new FakeEncryptor();
        var cut = new PackageHeaderSerializer(fake);
        var header = new PackageHeader(Unknown: unknown, FileCount: 0, ExtraFileCount: 0);
        var destination = new byte[32];

        cut.Serialize(header, 0, 0, destination);

        var value = BinaryPrimitives.ReadInt32LittleEndian(fake.LastPlaintext!.AsSpan(4));
        await Assert.That(value).IsEqualTo(unknown);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(999)]
    public async Task Serialize_PlaintextHasCorrectFileCountAtOffset8(int fileCount)
    {
        var fake = new FakeEncryptor();
        var cut = new PackageHeaderSerializer(fake);
        var header = new PackageHeader(Unknown: 0, FileCount: 0, ExtraFileCount: 0);
        var destination = new byte[32];

        cut.Serialize(header, fileCount, 0, destination);

        var value = BinaryPrimitives.ReadUInt32LittleEndian(fake.LastPlaintext!.AsSpan(8));
        await Assert.That((int)value).IsEqualTo(fileCount);
    }

    [Test]
    [Arguments(0)]
    [Arguments(7)]
    [Arguments(500)]
    public async Task Serialize_PlaintextHasCorrectExtraFileCountAtOffset12(int extraFileCount)
    {
        var fake = new FakeEncryptor();
        var cut = new PackageHeaderSerializer(fake);
        var header = new PackageHeader(Unknown: 0, FileCount: 0, ExtraFileCount: 0);
        var destination = new byte[32];

        cut.Serialize(header, 0, extraFileCount, destination);

        var value = BinaryPrimitives.ReadUInt32LittleEndian(fake.LastPlaintext!.AsSpan(12));
        await Assert.That((int)value).IsEqualTo(extraFileCount);
    }

    [Test]
    public async Task Serialize_OutputIsWrittenToDestination()
    {
        var fake = new FakeEncryptor();
        var cut = new PackageHeaderSerializer(fake);
        var header = new PackageHeader(Unknown: 99, FileCount: 0, ExtraFileCount: 0);
        var destination = new byte[32];

        cut.Serialize(header, 0, 0, destination);

        // The identity encryptor copies plaintext to destination, so they should match.
        await Assert.That(destination).IsSequenceEqualTo(fake.LastPlaintext!);
    }
}