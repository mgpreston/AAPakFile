namespace AAPakFile.Chunking;

/// <summary>
/// Controls the pipe and block-size parameters used by <see cref="ChunkedFileProcessor"/>.
/// </summary>
public sealed record ChunkedFileProcessorOptions
{
    /// <summary>
    /// Gets the maximum number of bytes read from the package per pipe write. Also controls pipe
    /// backpressure: the writer pauses at 2× this value and resumes at 1×. Defaults to 5 MiB.
    /// </summary>
    public int MaxBlockSize { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// Gets the default options instance, using all default values.
    /// </summary>
    public static ChunkedFileProcessorOptions Default { get; } = new();
}