using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Integrity;

/// <summary>
/// Defines an interface for verifying the integrity of multiple packed files.
/// </summary>
/// <remarks>
/// This interface is optimized for multiple packed files.
/// For single packed files, consider using <see cref="IPackageIntegrityVerifier"/>.
/// </remarks>
/// <seealso cref="IPackageIntegrityVerifier"/>
public interface IBulkPackageIntegrityVerifier
{
    /// <summary>
    /// Verifies the integrity of a collection of packed files.
    /// </summary>
    /// <param name="packageHandle">A handle to the package file.</param>
    /// <param name="fileRecords">The records of the packed files to verify.</param>
    /// <param name="cancellationToken">A cancellation token to request cancellation.</param>
    /// <returns>An asynchronous iteration over the results of the integrity check.</returns>
    IAsyncEnumerable<BulkPackedFileIntegrityResult> VerifyAsync(SafeFileHandle packageHandle,
        IEnumerable<PackedFileRecord> fileRecords, CancellationToken cancellationToken);
}