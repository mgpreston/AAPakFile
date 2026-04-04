namespace AAPakFile.Integrity;

/// <summary>
/// The result of verifying the integrity of a single packed file as part of a batch verification.
/// </summary>
/// <param name="Record">The file record of the file being verified.</param>
/// <param name="IsFileIntegrityIntact">Whether the file's integrity is intact.</param>
/// <param name="ProcessedFilesCount">The number of files that have been processed.</param>
/// <param name="IntactFilesCount">The number of processed files whose integrity was intact.</param>
/// <param name="TotalFilesCount">The total number of files to be processed.</param>
public record BulkPackedFileIntegrityResult(
    PackedFileRecord Record,
    bool IsFileIntegrityIntact,
    int ProcessedFilesCount,
    int IntactFilesCount,
    int TotalFilesCount);