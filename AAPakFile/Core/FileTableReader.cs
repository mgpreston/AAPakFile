using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AAPakFile.Core;

/// <summary>
/// Supports reading of the file table from a <see cref="Stream"/> representing a package file.
/// </summary>
public class FileTableReader(IDecryptor decryptor) : IFileTableReader
{
    /// <inheritdoc />
    public async IAsyncEnumerable<PackedFileRecord> ReadFileRecordsAsync(Stream stream, PackageHeader header,
        bool includeExtraFiles, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fileRecordSize = Unsafe.SizeOf<PackedFileRecord>();

        // Always use the full record count (active + extra) to compute the correct start of the file table.
        // Extra files are stored after normal files, so we can stop reading early to skip them.
        var allRecordCount = header.FileCount + header.ExtraFileCount;
        var totalFileInfoSize = fileRecordSize * allRecordCount;
        var firstFileInfoOffset = stream.Length - PackageFormat.BlockSize - totalFileInfoSize;
        var dif = firstFileInfoOffset % PackageFormat.BlockSize;
        // Align backward to the previous PackageFormat.BlockSize boundary
        firstFileInfoOffset -= dif;

        stream.Position = firstFileInfoOffset;

        using var fileRecordEncryptedOwner = MemoryPool<byte>.Shared.Rent(fileRecordSize);
        var fileRecordEncrypted = fileRecordEncryptedOwner.Memory[..fileRecordSize];
        using var fileRecordOwner = MemoryPool<byte>.Shared.Rent(fileRecordSize);
        var fileRecord = fileRecordOwner.Memory[..fileRecordSize];

        var countToRead = header.FileCount + (includeExtraFiles ? header.ExtraFileCount : 0);
        for (var i = 0; i < countToRead; i++)
        {
            await stream.ReadExactlyAsync(fileRecordEncrypted, cancellationToken);
            decryptor.Decrypt(fileRecordEncrypted.Span, fileRecord.Span);

            ref var record = ref MemoryMarshal.AsRef<PackedFileRecord>(fileRecord.Span);
            yield return record;
        }
    }
}