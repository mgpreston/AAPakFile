namespace AAPakFile.Reading;

/// <summary>
/// Defines an interface for reading the contents of a package file.
/// </summary>
public interface IPackageReader : IAsyncDisposable
{
    /// <summary>
    /// Gets a read-only list of all active files in the package, loaded when the reader was opened.
    /// </summary>
    IReadOnlyList<PackageReaderEntry> Entries { get; }
}