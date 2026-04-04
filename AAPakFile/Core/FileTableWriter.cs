using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AAPakFile.Core;

/// <summary>
/// Supports writing of the file table to a <see cref="Stream"/> representing a package file.
/// </summary>
public class FileTableWriter(IEncryptor encryptor) : IFileTableWriter
{
    /// <inheritdoc />
    public async Task WriteFileRecordsAsync(Stream stream, IEnumerable<PackedFileRecord> records,
        CancellationToken cancellationToken)
    {
        var fileRecordSize = Unsafe.SizeOf<PackedFileRecord>();

        using var plaintextOwner = MemoryPool<byte>.Shared.Rent(fileRecordSize);
        var plaintext = plaintextOwner.Memory[..fileRecordSize];

        using var ciphertextOwner = MemoryPool<byte>.Shared.Rent(fileRecordSize);
        var ciphertext = ciphertextOwner.Memory[..fileRecordSize];

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reinterpret the struct as a read-only byte span (zero-copy serialization via sequential layout)
            var recordRef = record;
            var recordBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref recordRef, 1));
            recordBytes.CopyTo(plaintext.Span);

            encryptor.Encrypt(plaintext.Span, ciphertext.Span);

            await stream.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
        }
    }
}