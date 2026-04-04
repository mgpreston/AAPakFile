using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Core;

/// <summary>
/// Default <see cref="IRandomAccessReader"/> implementation that delegates to the
/// <see cref="RandomAccess"/> static class.
/// </summary>
internal sealed class RandomAccessReader : IRandomAccessReader
{
    /// <summary>The shared singleton instance.</summary>
    public static readonly RandomAccessReader Instance = new();

    private RandomAccessReader() { }

    /// <inheritdoc />
    public long GetLength(SafeFileHandle handle) => RandomAccess.GetLength(handle);

    /// <inheritdoc />
    public int Read(SafeFileHandle handle, Span<byte> buffer, long fileOffset) =>
        RandomAccess.Read(handle, buffer, fileOffset);
}