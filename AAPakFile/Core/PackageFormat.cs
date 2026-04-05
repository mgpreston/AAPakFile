namespace AAPakFile.Core;

/// <summary>
/// Defines the fundamental layout constants of an ArcheAge package file.
/// </summary>
internal static class PackageFormat
{
    /// <summary>
    /// Block/sector size in bytes (512). All file data, file-table entries, and the header block
    /// are aligned to this boundary.
    /// </summary>
    internal const int BlockSize = 512;

    /// <summary>
    /// Size of the encrypted header blob at the end of each package, in bytes (32).
    /// The remaining <c>BlockSize - EncryptedHeaderSize</c> bytes in the header block are zeroed padding.
    /// </summary>
    internal const int EncryptedHeaderSize = 32;
}