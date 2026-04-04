using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Core;

/// <summary>
/// A stream implementation that provides read-only access to the contents of a single file contained by a package.
/// </summary>
/// <param name="packageHandle">A handle to the package containing the file.</param>
/// <param name="fileOffset">The offset in bytes from the start of the package to where the file contents begin.</param>
/// <param name="fileLength">The length of the file contents in bytes.</param>
/// <param name="ownsHandle">Whether this stream takes ownership of <paramref name="packageHandle"/> and disposes it when the stream is closed.</param>
public class PackedFileStream(SafeFileHandle packageHandle, long fileOffset, long fileLength, bool ownsHandle = false) : Stream
{
    private long _filePosition;

    /// <summary>
    /// Gets the current position within the package, based on the packed file offset and position within the packed
    /// file.
    /// </summary>
    private long PackagePosition => fileOffset + _filePosition;

    /// <summary>
    /// Gets the number of bytes remaining in the packed file, based on the current position.
    /// </summary>
    private long Remaining => fileLength - _filePosition;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => fileLength;

    /// <inheritdoc />
    public override long Position
    {
        get => _filePosition;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    public override void Flush()
    {
        // Do nothing. We don't support writing.
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        count = (int)Math.Min(count, Remaining);
        var read = RandomAccess.Read(packageHandle, buffer.AsSpan(offset, count), PackagePosition);
        _filePosition += read;
        return read;
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var count = (int)Math.Min(buffer.Length, Remaining);
        buffer = buffer[..count];
        var read = RandomAccess.Read(packageHandle, buffer, PackagePosition);
        _filePosition += read;
        return read;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _filePosition + offset,
            SeekOrigin.End => fileLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
        };

        newPosition = Math.Max(Math.Min(newPosition, fileLength), 0);

        _filePosition = newPosition;
        return newPosition;
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && ownsHandle) packageHandle.Dispose();
        base.Dispose(disposing);
    }
}