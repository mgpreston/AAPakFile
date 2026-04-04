namespace AAPakFile.Import;

/// <summary>
/// Represents progress information for an import operation.
/// </summary>
/// <param name="ImportedFilesCount">The number of files that have been successfully imported so far.</param>
/// <param name="TotalFilesCount">
/// The total number of files to import, or <see langword="null"/> if the total is not yet known
/// (for example, when the source is a lazily-enumerated directory tree).
/// </param>
public readonly record struct ImportProgress(int ImportedFilesCount, int? TotalFilesCount);