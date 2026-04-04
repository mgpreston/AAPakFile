namespace AAPakFile.Editing;

/// <summary>
/// Optional settings that control how a file is written to a package.
/// </summary>
public sealed record PackageWriteOptions
{
    /// <summary>
    /// The expected byte length of the data stream. Only meaningful when the data source is a
    /// non-seekable stream; ignored otherwise. Allows the editor to choose the best placement
    /// strategy (in-place replace, slot reuse, or append) before reading begins.
    /// </summary>
    /// <remarks>
    /// If the stream yields more bytes than this value the write is aborted and an
    /// <see cref="InvalidOperationException"/> is thrown before excess bytes are written.
    /// If the stream yields fewer bytes the shortfall is recorded as additional padding.
    /// </remarks>
    public long? SizeHint { get; init; }

    /// <summary>
    /// The creation timestamp to store in the package entry.
    /// Defaults to <see cref="DateTimeOffset.UtcNow"/> when <see langword="null"/>.
    /// When replacing an existing file and this property is <see langword="null"/>,
    /// the original creation time is preserved.
    /// </summary>
    public DateTimeOffset? CreationTime { get; init; }

    /// <summary>
    /// The modification timestamp to store in the package entry.
    /// Defaults to <see cref="DateTimeOffset.UtcNow"/> when <see langword="null"/>.
    /// </summary>
    public DateTimeOffset? ModifiedTime { get; init; }
}