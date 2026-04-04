using AAPakFile.Editing;

namespace AAPakFile.Import;

/// <summary>
/// Defines an interface for importing files from a directory in the file system into a package.
/// </summary>
public interface IFileImporter
{
    /// <summary>
    /// Asynchronously imports all files from the specified source folder into the package being edited.
    /// </summary>
    /// <param name="editor">The package editor to import files into.</param>
    /// <param name="sourceFolder">The path to the folder containing files to import.</param>
    /// <param name="progress">An optional progress reporter to receive updates as each file is imported.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous import operation.</returns>
    Task ImportAsync(IPackageEditor editor, string sourceFolder,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}