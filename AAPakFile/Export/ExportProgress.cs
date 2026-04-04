namespace AAPakFile.Export;

/// <summary>
/// Represents progress information for an export operation.
/// </summary>
/// <param name="ExportedFilesCount">The number of files that have been successfully exported.</param>
/// <param name="TotalFilesCount">The total number of files to export.</param>
public readonly record struct ExportProgress(int ExportedFilesCount, int TotalFilesCount);