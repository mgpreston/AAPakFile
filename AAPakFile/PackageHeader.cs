namespace AAPakFile;

/// <summary>
/// The header data of a package file.
/// </summary>
/// <param name="Unknown">An unknown integer value.</param>
/// <param name="FileCount">The number of files contained by the package.</param>
/// <param name="ExtraFileCount">The number of additional files (deleted files) contained by the package.</param>
public record PackageHeader(int Unknown, int FileCount, int ExtraFileCount)
{
    /// <summary>
    /// The 4-byte header magic that signifies the start of the package header.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> HeaderMagic = "WIBO"u8.ToArray();
}