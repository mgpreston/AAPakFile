namespace AAPakFile.Integrity;

/// <summary>
/// The progress of verifying the integrity of a package.
/// </summary>
/// <param name="ProcessedFilesCount">The number of files that have been processed.</param>
/// <param name="ValidFilesCount">The number of processed files whose integrity was intact.</param>
/// <param name="TotalFilesCount">The total number of files to be processed.</param>
public record VerifyProgress(int ProcessedFilesCount, int ValidFilesCount, int TotalFilesCount);