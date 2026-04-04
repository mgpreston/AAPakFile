using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AAPakFile.Core;

public class FileTableWriterTests
{
    private class FakeEncryptor : IEncryptor
    {
        private readonly List<byte[]> _plaintexts = [];

        public IReadOnlyList<byte[]> Plaintexts => _plaintexts;

        public void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> destination)
        {
            _plaintexts.Add(plaintext.ToArray());
            // Identity cipher: copy plaintext to destination unchanged.
            plaintext.CopyTo(destination);
        }
    }

    private static PackedFileRecord MakeRecord(string name, long offset = 0) =>
        new(FileName: PackedFileRecord.FileNameBuffer.FromString(name),
            FileOffset: offset, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

    [Test]
    public async Task WriteFileRecordsAsync_EmptyRecords_WritesNothing(CancellationToken cancellationToken)
    {
        var fake = new FakeEncryptor();
        var cut = new FileTableWriter(fake);
        using var stream = new MemoryStream();

        await cut.WriteFileRecordsAsync(stream, [], cancellationToken);

        await Assert.That(stream.Length).IsEqualTo(0L);
        await Assert.That(fake.Plaintexts).IsEmpty();
    }

    [Test]
    public async Task WriteFileRecordsAsync_WritesOneEncryptedBlockPerRecord(CancellationToken cancellationToken)
    {
        var fake = new FakeEncryptor();
        var cut = new FileTableWriter(fake);
        using var stream = new MemoryStream();

        var records = new[] { MakeRecord("a.txt"), MakeRecord("b.txt"), MakeRecord("c.txt") };
        await cut.WriteFileRecordsAsync(stream, records, cancellationToken);

        var expectedSize = (long)Unsafe.SizeOf<PackedFileRecord>() * 3;
        await Assert.That(stream.Length).IsEqualTo(expectedSize);
    }

    [Test]
    public async Task WriteFileRecordsAsync_CallsEncryptorForEachRecord(CancellationToken cancellationToken)
    {
        var fake = new FakeEncryptor();
        var cut = new FileTableWriter(fake);
        using var stream = new MemoryStream();

        var records = new[] { MakeRecord("a.txt"), MakeRecord("b.txt") };
        await cut.WriteFileRecordsAsync(stream, records, cancellationToken);

        await Assert.That(fake.Plaintexts).Count().IsEqualTo(2);
    }

    [Test]
    public async Task WriteFileRecordsAsync_EncryptsCorrectRecordBytes(CancellationToken cancellationToken)
    {
        var fake = new FakeEncryptor();
        var cut = new FileTableWriter(fake);
        using var stream = new MemoryStream();

        var record = MakeRecord("test.txt", offset: 9876);
        await cut.WriteFileRecordsAsync(stream, [record], cancellationToken);

        var plaintext = await Assert.That(fake.Plaintexts).HasSingleItem();

        // Deserialize the plaintext back into a PackedFileRecord and verify the fields.
        var deserialized = MemoryMarshal.AsRef<PackedFileRecord>(plaintext.AsSpan());
        await Assert.That(deserialized.FileName.ToString()).IsEqualTo("test.txt");
        await Assert.That(deserialized.FileOffset).IsEqualTo(9876L);
    }

    [Test]
    public async Task WriteFileRecordsAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var fake = new FakeEncryptor();
        var cut = new FileTableWriter(fake);
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await cut.WriteFileRecordsAsync(stream, [MakeRecord("x.txt")], new CancellationToken(canceled: true)));
    }
}