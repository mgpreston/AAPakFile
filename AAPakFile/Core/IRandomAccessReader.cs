using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Core;

/// <summary>
/// Abstracts the static <see cref="RandomAccess"/> methods used by <see cref="FilePackageHeaderReader"/>
/// so that callers can be tested without real file I/O.
/// </summary>
internal interface IRandomAccessReader
{
    /// <summary>Returns the length of the file referred to by <paramref name="handle"/>.</summary>
    long GetLength(SafeFileHandle handle);

    /// <summary>
    /// Reads bytes from <paramref name="handle"/> at the given <paramref name="fileOffset"/> into
    /// <paramref name="buffer"/> and returns the number of bytes actually read.
    /// Returns 0 when the offset is at or beyond end-of-file.
    /// </summary>
    int Read(SafeFileHandle handle, Span<byte> buffer, long fileOffset);
}