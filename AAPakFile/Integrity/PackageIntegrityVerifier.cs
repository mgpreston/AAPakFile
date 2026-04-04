using System.Buffers;
using System.Security.Cryptography;

using AAPakFile.Core;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Integrity;

/// <summary>
/// This class provides methods for verifying the integrity of individual packed files.
/// </summary>
/// <remarks>
/// This class is optimized for single packed files.
/// For multiple packed files, consider using <see cref="BulkPackageIntegrityVerifier"/>.
/// </remarks>
/// <seealso cref="BulkPackageIntegrityVerifier"/>
public class PackageIntegrityVerifier : IPackageIntegrityVerifier
{
    /// <inheritdoc />
    public async Task<bool> VerifyAsync(PackedFileRecord fileRecord, SafeFileHandle packageHandle,
        int bufferSize = 160 * 1024, CancellationToken cancellationToken = default)
    {
        await using var stream = new PackedFileStream(packageHandle, fileRecord.FileOffset, fileRecord.StoredSize);

        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(bufferSize);
        var buffer = bufferOwner.Memory[..bufferSize];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            incrementalHash.AppendData(buffer.Span[..bytesRead]);
        }

        Span<byte> calculatedHash = stackalloc byte[incrementalHash.HashLengthInBytes];
        _ = incrementalHash.GetCurrentHash(calculatedHash);

        return calculatedHash.SequenceEqual(fileRecord.Md5.AsSpan());
    }
}