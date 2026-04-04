using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AAPakFile.Core;

public class FileTableReaderTests
{
    // Identity cipher: copies ciphertext directly to destination and records call count.
    private class FakeDecryptor : IDecryptor
    {
        public int CallCount { get; private set; }

        public void Decrypt(ReadOnlySpan<byte> ciphertext, Span<byte> destination)
        {
            ciphertext.CopyTo(destination);
            CallCount++;
        }
    }

    /// <summary>
    /// Builds a MemoryStream whose contents are laid out so that FileTableReader, when given
    /// <paramref name="header"/>, finds <paramref name="records"/> at the correct aligned offset.
    /// The file table always starts at the same position regardless of <paramref name="includeExtraFiles"/>;
    /// that parameter is unused here and exists only for call-site clarity.
    /// </summary>
    private static MemoryStream CreateStreamWithRecords(
        PackedFileRecord[] records,
        PackageHeader header,
        bool includeExtraFiles)
    {
        // includeExtraFiles is intentionally unused: the stream layout is the same either way.
        _ = includeExtraFiles;
        var fileRecordSize = Unsafe.SizeOf<PackedFileRecord>();
        // Always use the full record count so the offset matches the actual on-disk layout.
        var allRecordCount = header.FileCount + header.ExtraFileCount;

        // Ensure the stream is big enough: header block (512) + all records + at least one extra block.
        var minLength = 512 + fileRecordSize * allRecordCount + 512;
        var streamLength = ((minLength + 511) / 512) * 512;

        // Mirror the formula from FileTableReader (always uses allRecordCount).
        var firstFileInfoOffset = (long)streamLength - 512 - (long)fileRecordSize * allRecordCount;
        var dif = firstFileInfoOffset % 0x200;
        firstFileInfoOffset -= dif;

        var buffer = new byte[streamLength];

        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];
            var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref record, 1));
            span.CopyTo(buffer.AsSpan((int)(firstFileInfoOffset + (long)i * fileRecordSize)));
        }

        return new MemoryStream(buffer);
    }

    [Test]
    public async Task ReadFileRecordsAsync_ZeroFiles_ReturnsEmpty(CancellationToken cancellationToken)
    {
        var fake = new FakeDecryptor();
        var cut = new FileTableReader(fake);
        var header = new PackageHeader(Unknown: 0, FileCount: 0, ExtraFileCount: 0);

        // Zero-file stream: minimal 512-byte stream (one header block).
        using var stream = new MemoryStream(new byte[512]);

        var results = await cut.ReadFileRecordsAsync(stream, header, includeExtraFiles: false, cancellationToken)
            .ToListAsync(cancellationToken);

        await Assert.That(results).IsEmpty();
        await Assert.That(fake.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task ReadFileRecordsAsync_OneFile_ReturnsRecord_WithCorrectFields(CancellationToken cancellationToken)
    {
        var expected = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("hello.txt"),
            FileOffset: 12345,
            FileSize: 100,
            StoredSize: 100,
            PaddingSize: 24,
            Md5: default,
            Reserved1: 0,
            CreationTime: default,
            ModifiedTime: default,
            AesPadding: 0);

        var header = new PackageHeader(Unknown: 0, FileCount: 1, ExtraFileCount: 0);
        var fake = new FakeDecryptor();
        var cut = new FileTableReader(fake);

        using var stream = CreateStreamWithRecords([expected], header, includeExtraFiles: false);

        var results = await cut.ReadFileRecordsAsync(stream, header, includeExtraFiles: false, cancellationToken)
            .ToListAsync(cancellationToken);

        var actual = await Assert.That(results).HasSingleItem();
        await Assert.That(actual.FileName.ToString()).IsEqualTo("hello.txt");
        await Assert.That(actual.FileOffset).IsEqualTo(12345L);
        await Assert.That(actual.FileSize).IsEqualTo(100L);
        await Assert.That(actual.StoredSize).IsEqualTo(100L);
        await Assert.That(actual.PaddingSize).IsEqualTo(24);
    }

    [Test]
    public async Task ReadFileRecordsAsync_ExcludesExtraFiles_WhenFalse(CancellationToken cancellationToken)
    {
        var normalRecord = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("normal.txt"),
            FileOffset: 0, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

        // header.FileCount=1, ExtraFileCount=1 but we read with includeExtraFiles=false
        var header = new PackageHeader(Unknown: 0, FileCount: 1, ExtraFileCount: 1);
        var fake = new FakeDecryptor();
        var cut = new FileTableReader(fake);

        // Only pass the normal record; the extra slot is zeroed in the stream buffer.
        using var stream = CreateStreamWithRecords([normalRecord], header, includeExtraFiles: false);

        var results = await cut.ReadFileRecordsAsync(stream, header, includeExtraFiles: false, cancellationToken)
            .ToListAsync(cancellationToken);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].FileName.ToString()).IsEqualTo("normal.txt");
    }

    [Test]
    public async Task ReadFileRecordsAsync_IncludesExtraFiles_WhenTrue(CancellationToken cancellationToken)
    {
        var normalRecord = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("normal.txt"),
            FileOffset: 0, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

        var extraRecord = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("__unused__"),
            FileOffset: 512, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

        var header = new PackageHeader(Unknown: 0, FileCount: 1, ExtraFileCount: 1);
        var fake = new FakeDecryptor();
        var cut = new FileTableReader(fake);

        using var stream = CreateStreamWithRecords([normalRecord, extraRecord], header, includeExtraFiles: true);

        var results = await cut.ReadFileRecordsAsync(stream, header, includeExtraFiles: true, cancellationToken)
            .ToListAsync(cancellationToken);

        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results[0].FileName.ToString()).IsEqualTo("normal.txt");
        await Assert.That(results[1].FileName.ToString()).IsEqualTo("__unused__");
    }

    [Test]
    public async Task ReadFileRecordsAsync_CallsDecryptorOncePerRecord(CancellationToken cancellationToken)
    {
        var header = new PackageHeader(Unknown: 0, FileCount: 3, ExtraFileCount: 0);
        var records = Enumerable.Range(0, 3).Select(i => new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString($"file{i}.txt"),
            FileOffset: i * 512, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0))
            .ToArray();

        var fake = new FakeDecryptor();
        var cut = new FileTableReader(fake);

        using var stream = CreateStreamWithRecords(records, header, includeExtraFiles: false);

        _ = await cut.ReadFileRecordsAsync(stream, header, includeExtraFiles: false, cancellationToken)
            .ToListAsync(cancellationToken);

        await Assert.That(fake.CallCount).IsEqualTo(3);
    }
}