using AAPakFile.Editing;

namespace AAPakFile.Import;

/// <summary>
/// Extension methods for <see cref="FileImporter"/> and <see cref="ZipImporter"/>.
/// </summary>
public static class ImporterExtensions
{
    /// <param name="importer">The <see cref="FileImporter"/> instance used for importing files.</param>
    extension(FileImporter importer)
    {
        /// <summary>
        /// Imports all files from the specified source folder into a package, then saves the package.
        /// </summary>
        /// <param name="packagePath">The path to the package file to import into.</param>
        /// <param name="sourceFolder">The path to the folder containing files to import.</param>
        /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
        /// <param name="progress">An optional progress reporter to receive updates as each file is imported.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous import operation.</returns>
        public async Task ImportAllFromFolderAsync(string packagePath, string sourceFolder,
            ReadOnlyMemory<byte> xlGamesKey = default,
            IProgress<ImportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await using var editor = await PackageEditor.OpenAsync(packagePath, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await importer.ImportAsync(editor, sourceFolder, progress, cancellationToken)
                .ConfigureAwait(false);
            await editor.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new package file from the specified source folder, then saves the package.
        /// If a file already exists at <paramref name="packagePath"/> it is overwritten.
        /// </summary>
        /// <param name="packagePath">The path of the package file to create.</param>
        /// <param name="sourceFolder">The path to the folder containing files to import.</param>
        /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
        /// <param name="progress">An optional progress reporter to receive updates as each file is imported.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous create operation.</returns>
        public async Task CreateFromFolderAsync(string packagePath, string sourceFolder,
            ReadOnlyMemory<byte> xlGamesKey = default,
            IProgress<ImportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await using var editor = await PackageEditor.CreateAsync(packagePath, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await importer.ImportAsync(editor, sourceFolder, progress, cancellationToken)
                .ConfigureAwait(false);
            await editor.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <param name="importer">The <see cref="ZipImporter"/> instance used for importing files.</param>
    extension(ZipImporter importer)
    {
        /// <summary>
        /// Imports all files from the specified ZIP archive into a package, then saves the package.
        /// </summary>
        /// <param name="packagePath">The path to the package file to import into.</param>
        /// <param name="zipFilePath">The path to the ZIP archive containing files to import.</param>
        /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
        /// <param name="progress">An optional progress reporter to receive updates as each file is imported.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous import operation.</returns>
        public async Task ImportAllFromZipArchiveAsync(string packagePath, string zipFilePath,
            ReadOnlyMemory<byte> xlGamesKey = default,
            IProgress<ImportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await using var editor = await PackageEditor.OpenAsync(packagePath, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await importer.ImportAsync(editor, zipFilePath, progress, cancellationToken)
                .ConfigureAwait(false);
            await editor.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new package file from the specified ZIP archive, then saves the package.
        /// If a file already exists at <paramref name="packagePath"/> it is overwritten.
        /// </summary>
        /// <param name="packagePath">The path of the package file to create.</param>
        /// <param name="zipFilePath">The path to the ZIP archive containing files to import.</param>
        /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
        /// <param name="progress">An optional progress reporter to receive updates as each file is imported.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous create operation.</returns>
        public async Task CreateFromZipArchiveAsync(string packagePath, string zipFilePath,
            ReadOnlyMemory<byte> xlGamesKey = default,
            IProgress<ImportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await using var editor = await PackageEditor.CreateAsync(packagePath, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await importer.ImportAsync(editor, zipFilePath, progress, cancellationToken)
                .ConfigureAwait(false);
            await editor.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}