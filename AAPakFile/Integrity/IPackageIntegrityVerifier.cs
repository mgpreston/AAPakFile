using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Integrity;

/// <summary>
/// Defines an interface for verifying the integrity of individual packed files.
/// </summary>
/// <remarks>
/// This interface is optimized for single packed files.
/// For multiple packed files, consider using <see cref="IBulkPackageIntegrityVerifier"/>.
/// </remarks>
/// <seealso cref="IBulkPackageIntegrityVerifier"/>
public interface IPackageIntegrityVerifier
{
    /// <summary>
    /// Verifies the integrity of a single packed file.
    /// </summary>
    /// <param name="fileRecord">The record of the packed file to verify.</param>
    /// <param name="packageHandle">A handle to the package file.</param>
    /// <param name="bufferSize">The size of the buffer to use when reading the file.</param>
    /// <param name="cancellationToken">A cancellation token to request cancellation.</param>
    /// <returns><c>true</c> if the file was verified to be intact; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method is optimized for random access for single game files. It is not optimized for verifying the
    /// integrity of the entire package.
    /// </remarks>
    Task<bool> VerifyAsync(PackedFileRecord fileRecord, SafeFileHandle packageHandle,
        int bufferSize = 160 * 1024, CancellationToken cancellationToken = default);
}