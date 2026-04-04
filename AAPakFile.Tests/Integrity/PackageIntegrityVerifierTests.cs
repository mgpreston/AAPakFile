using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AAPakFile.Integrity;

public class PackageIntegrityVerifierTests
{
    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    private static PackedFileRecord.Md5Buffer ComputeMd5(byte[] data)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(data, hash);

        var md5 = new PackedFileRecord.Md5Buffer();
        hash.CopyTo(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref md5, 1)));
        return md5;
    }

    [Test]
    public async Task VerifyAsync_CorrectMd5_ReturnsTrue(CancellationToken cancellationToken)
    {
        var data = "integrity test content"u8.ToArray();
        var md5 = ComputeMd5(data);

        var path = NewTempPath();
        try
        {
            await File.WriteAllBytesAsync(path, data, cancellationToken);

            var record = new PackedFileRecord(
                FileName: PackedFileRecord.FileNameBuffer.FromString("test.bin"),
                FileOffset: 0,
                FileSize: data.Length,
                StoredSize: data.Length,
                PaddingSize: 0,
                Md5: md5,
                Reserved1: 0,
                CreationTime: default,
                ModifiedTime: default,
                AesPadding: 0);

            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            var verifier = new PackageIntegrityVerifier();
            var result = await verifier.VerifyAsync(record, handle,
                cancellationToken: cancellationToken);

            await Assert.That(result).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task VerifyAsync_IncorrectMd5_ReturnsFalse(CancellationToken cancellationToken)
    {
        var data = "real content"u8.ToArray();
        var wrongMd5 = ComputeMd5("different content"u8.ToArray());

        var path = NewTempPath();
        try
        {
            await File.WriteAllBytesAsync(path, data, cancellationToken);

            var record = new PackedFileRecord(
                FileName: PackedFileRecord.FileNameBuffer.FromString("wrong.bin"),
                FileOffset: 0,
                FileSize: data.Length,
                StoredSize: data.Length,
                PaddingSize: 0,
                Md5: wrongMd5,
                Reserved1: 0,
                CreationTime: default,
                ModifiedTime: default,
                AesPadding: 0);

            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            var verifier = new PackageIntegrityVerifier();
            var result = await verifier.VerifyAsync(record, handle,
                cancellationToken: cancellationToken);

            await Assert.That(result).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }
}