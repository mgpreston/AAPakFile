using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AAPakFile.Core;

public class FileTableReaderExtensionsTests
{
    private class FakeDecryptor : IDecryptor
    {
        public void Decrypt(ReadOnlySpan<byte> ciphertext, Span<byte> destination) =>
            ciphertext.CopyTo(destination);
    }

    private static MemoryStream CreateStreamWithRecord(PackedFileRecord record)
    {
        var fileRecordSize = Unsafe.SizeOf<PackedFileRecord>();
        var minLength = 512 + fileRecordSize + 512;
        var streamLength = (minLength + 511) / 512 * 512;

        var firstFileInfoOffset = (long)streamLength - 512 - fileRecordSize;
        var dif = firstFileInfoOffset % 0x200;
        firstFileInfoOffset -= dif;

        var buffer = new byte[streamLength];
        var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref record, 1));
        span.CopyTo(buffer.AsSpan((int)firstFileInfoOffset));

        return new MemoryStream(buffer);
    }

    [Test]
    public async Task FindFileRecordAsync_ExistingFile_ReturnsRecord(CancellationToken cancellationToken)
    {
        var record = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("data/config.xml"),
            FileOffset: 100, FileSize: 50, StoredSize: 50, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

        var header = new PackageHeader(Unknown: 0, FileCount: 1, ExtraFileCount: 0);
        var reader = new FileTableReader(new FakeDecryptor());
        using var stream = CreateStreamWithRecord(record);

        var found = await reader.FindFileRecordAsync(stream, header, "data/config.xml", cancellationToken: cancellationToken);

        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Value.FileName.ToString()).IsEqualTo("data/config.xml");
        await Assert.That(found.Value.FileOffset).IsEqualTo(100L);
    }

    [Test]
    public async Task FindFileRecordAsync_NonExistentFile_ReturnsNull(CancellationToken cancellationToken)
    {
        var record = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("existing.txt"),
            FileOffset: 0, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

        var header = new PackageHeader(Unknown: 0, FileCount: 1, ExtraFileCount: 0);
        var reader = new FileTableReader(new FakeDecryptor());
        using var stream = CreateStreamWithRecord(record);

        var found = await reader.FindFileRecordAsync(stream, header, "missing.txt", cancellationToken: cancellationToken);

        await Assert.That(found).IsNull();
    }

    [Test]
    public async Task FindFileRecordAsync_CaseSensitiveComparison_ReturnsNull_WhenCaseDiffers(CancellationToken cancellationToken)
    {
        var record = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("File.TXT"),
            FileOffset: 0, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

        var header = new PackageHeader(Unknown: 0, FileCount: 1, ExtraFileCount: 0);
        var reader = new FileTableReader(new FakeDecryptor());
        using var stream = CreateStreamWithRecord(record);

        var found = await reader.FindFileRecordAsync(stream, header, "file.txt",
            cancellationToken: cancellationToken);

        await Assert.That(found).IsNull();
    }

    [Test]
    public async Task FindFileRecordAsync_CaseInsensitiveComparison_FindsRecord(CancellationToken cancellationToken)
    {
        var record = new PackedFileRecord(
            FileName: PackedFileRecord.FileNameBuffer.FromString("File.TXT"),
            FileOffset: 0, FileSize: 10, StoredSize: 10, PaddingSize: 0,
            Md5: default, Reserved1: 0, CreationTime: default, ModifiedTime: default, AesPadding: 0);

        var header = new PackageHeader(Unknown: 0, FileCount: 1, ExtraFileCount: 0);
        var reader = new FileTableReader(new FakeDecryptor());
        using var stream = CreateStreamWithRecord(record);

        var found = await reader.FindFileRecordAsync(stream, header, "file.txt",
            StringComparison.OrdinalIgnoreCase, cancellationToken: cancellationToken);

        await Assert.That(found).IsNotNull();
    }
}