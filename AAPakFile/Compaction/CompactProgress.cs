namespace AAPakFile.Compaction;

/// <summary>
/// Reports progress during a package compaction operation.
/// </summary>
/// <param name="ProcessedFilesCount">The number of files that have been processed so far.</param>
/// <param name="TotalFilesCount">The total number of files to process.</param>
public readonly record struct CompactProgress(int ProcessedFilesCount, int TotalFilesCount);