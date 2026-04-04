using System.Runtime.CompilerServices;

using AAPakFile.Core;

namespace AAPakFile.Integrity;

/// <summary>
/// Extension methods for <see cref="IBulkPackageIntegrityVerifier"/>.
/// </summary>
public static class IntegrityExtensions
{
    /// <param name="integrityVerifier">The object used to perform the verification.</param>
    extension(IBulkPackageIntegrityVerifier integrityVerifier)
    {
        /// <summary>
        /// Verifies the integrity of all files in the specified package by checking each file's MD5 hash.
        /// All files are verified, even if damaged files are encountered.
        /// </summary>
        /// <param name="packagePath">The path to the package file to verify.</param>
        /// <param name="xlGamesKey">The AES decryption key for the package.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>An asynchronous iteration over the results of the integrity check.</returns>
        public async IAsyncEnumerable<BulkPackedFileIntegrityResult> VerifyAllFilesAsync(string packagePath,
            ReadOnlyMemory<byte> xlGamesKey = default,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var packageHandle = File.OpenHandle(packagePath, options: FileOptions.Asynchronous);

            var fileRecords = await FileTableHelper
                .LoadRecordsAsync(packageHandle, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await foreach (var result in integrityVerifier.VerifyAsync(packageHandle, fileRecords,
                               cancellationToken))
            {
                yield return result;
            }
        }

        /// <summary>
        /// Verifies the integrity of all files in the specified package by checking each file's MD5 hash.
        /// Stops on the first invalid file.
        /// </summary>
        /// <param name="packagePath">The path to the package file to verify.</param>
        /// <param name="xlGamesKey">The AES decryption key for the package.</param>
        /// <param name="progress">Receives progress updates as files are verified.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns> A <see cref="PackageIntegrityResult"/> containing the result of the verification.</returns>
        public async Task<PackageIntegrityResult> VerifyPackageAsync(string packagePath,
            ReadOnlyMemory<byte> xlGamesKey = default, IProgress<VerifyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await foreach (var result in integrityVerifier.VerifyAllFilesAsync(packagePath, xlGamesKey,
                               cancellationToken))
            {
                if (!result.IsFileIntegrityIntact)
                {
                    return new PackageIntegrityResult(false, result.Record);
                }

                progress?.Report(new VerifyProgress(result.ProcessedFilesCount, result.IntactFilesCount,
                    result.TotalFilesCount));
            }

            return new PackageIntegrityResult(Success: true, null);
        }
    }
}