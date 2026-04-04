using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace AAPakFile;

/// <summary>
/// A record containing metadata for a packed file (a virtual file contained within a package file).
/// </summary>
/// <param name="FileName">The name of the file, including path information.</param>
/// <param name="FileOffset">The offset within the package where the file contents start.</param>
/// <param name="FileSize">
/// The logical (uncompressed) size of the file. Used only for empty-record detection; never used as the actual
/// read limit when extracting file data.
/// </param>
/// <param name="StoredSize">
/// The physically stored size of the file data within the package. This is the actual number of bytes to read
/// when extracting the file. For all known uncompressed game_pak files, this equals <see cref="FileSize"/>.
/// </param>
/// <param name="PaddingSize">
/// The number of unused bytes immediately after the file data within the package. Together with
/// <see cref="FileSize"/>, this defines the total on-disk block: <c>FileSize + PaddingSize</c> is always a
/// multiple of 512, rounding up to fill the final block.
/// </param>
/// <param name="Md5">The MD5 hash of the file contents.</param>
/// <param name="Reserved1">Reserved; always zero in all known files.</param>
/// <param name="CreationTime">The date and time at which the file was created.</param>
/// <param name="ModifiedTime">The date and time at which the file was last modified.</param>
/// <param name="AesPadding">
/// Padding to align the struct to a multiple of 16 bytes (one AES block), as records are encrypted
/// individually. Always zero.
/// </param>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct PackedFileRecord(
    PackedFileRecord.FileNameBuffer FileName,
    long FileOffset,
    long FileSize,
    long StoredSize,
    int PaddingSize,
    PackedFileRecord.Md5Buffer Md5,
    uint Reserved1,
    PackedFileRecord.WindowsFileTime CreationTime,
    PackedFileRecord.WindowsFileTime ModifiedTime,
    ulong AesPadding)
{
    /// <summary>
    /// A struct of length 264 bytes containing ASCII text.
    /// </summary>
    [InlineArray(MaxLength)]
    public struct FileNameBuffer : IEquatable<FileNameBuffer>
    {
        /// <summary>
        /// The maximum length of the name of a packed file.
        /// </summary>
        public const int MaxLength = 264;

        private byte _element0;

        /// <summary>
        /// Writes the fixed-length ASCII file name to the destination span.
        /// </summary>
        /// <param name="destination">A UTF-16 encoded character span.</param>
        /// <param name="charsWritten">
        /// When the method returns, the number of characters written to <paramref name="destination"/>.
        /// </param>
        public void WriteTo(Span<char> destination, out int charsWritten)
        {
            var length = Length;
            ReadOnlySpan<byte> bytes = this;
            WriteTo(bytes[..length], destination[..length]);
            charsWritten = length;
        }

        /// <summary>
        /// Gets the length of the file name in bytes/chars (equivalent due to ASCII-encoding), without trailing null
        /// characters.
        /// </summary>
        public readonly int Length
        {
            get
            {
                ReadOnlySpan<byte> bytes = this;
                // There may be trailing nulls, so find the first and treat this as the end of the text
                var length = bytes.IndexOf((byte)0);
                return length < 0 ? bytes.Length : length;
            }
        }

        /// <summary>
        /// Returns the fixed-length ASCII file name without trailing null characters.
        /// </summary>
        /// <returns>The fixed-length ASCII file name without trailing null characters.</returns>
        public override string ToString()
        {
            var length = Length;
            ReadOnlySpan<byte> bytes = this;
            return string.Create(length, bytes[..length], static (destinationSpan, sourceSpan) =>
                WriteTo(sourceSpan, destinationSpan));
        }

        /// <summary>
        /// Converts from a <see cref="FileNameBuffer"/> struct to a string representation of the same struct.
        /// </summary>
        /// <param name="buffer">The file name buffer to convert to a string.</param>
        /// <returns>The fixed-length ASCII file name without trailing null characters.</returns>
        public static implicit operator string(FileNameBuffer buffer) => buffer.ToString();

        /// <inheritdoc />
        public bool Equals(FileNameBuffer other)
        {
            ReadOnlySpan<byte> thisBytes = this;
            ReadOnlySpan<byte> otherBytes = other;
            return thisBytes.SequenceEqual(otherBytes);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is FileNameBuffer other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.AddBytes(this);
            return hashCode.ToHashCode();
        }

        /// <summary>Determines whether two <see cref="FileNameBuffer"/> instances are equal.</summary>
        public static bool operator ==(FileNameBuffer left, FileNameBuffer right) => left.Equals(right);

        /// <summary>Determines whether two <see cref="FileNameBuffer"/> instances are not equal.</summary>
        public static bool operator !=(FileNameBuffer left, FileNameBuffer right) => !(left == right);

        /// <summary>
        /// Creates a <see cref="FileNameBuffer"/> from the specified string, encoding it as ASCII.
        /// The string is truncated to <see cref="MaxLength"/> - 1 characters to ensure null-termination.
        /// </summary>
        /// <param name="name">The file name to encode.</param>
        /// <returns>A <see cref="FileNameBuffer"/> containing the ASCII-encoded file name.</returns>
        public static FileNameBuffer FromString(string name)
        {
            var buffer = new FileNameBuffer();
            Span<byte> dest = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref buffer, 1));
            // Truncate to MaxLength - 1 to ensure the buffer is always null-terminated
            var count = Math.Min(name.Length, MaxLength - 1);
            Encoding.ASCII.GetBytes(name.AsSpan()[..count], dest);
            return buffer;
        }

        private static void WriteTo(ReadOnlySpan<byte> sourceBytes, Span<char> destination)
        {
            // All ASCII characters are also valid UTF-8.
            var status = Utf8.ToUtf16(sourceBytes, destination, out _, out _, replaceInvalidSequences: false);

            if (status != OperationStatus.Done)
            {
                throw new InvalidDataException("Name is likely not ASCII-encoded.");
            }
        }
    }

    /// <summary>
    /// A struct of length 16 bytes containing an MD5 hash.
    /// </summary>
    [InlineArray(16)]
    public struct Md5Buffer
    {
        private byte _element0;

        /// <summary>
        /// Returns the MD5 hash as a hexadecimal string.
        /// </summary>
        /// <returns>The MD5 hash as a hexadecimal string.</returns>
        public override string ToString() => Convert.ToHexString(this);

        /// <summary>
        /// Reads the MD5 hash as a read-only span of bytes.
        /// </summary>
        public readonly ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(in _element0, 16);
    }

    /// <summary>
    /// A struct representing a Windows file time, expressed in ticks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WindowsFileTime
    {
        /// <summary>
        /// The Windows file time expressed in ticks.
        /// </summary>
        public long Value;

        /// <summary>
        /// Reads the Windows file time as a <see cref="DateTimeOffset"/>, with the offset set to the local time offset.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <see cref="Value"/> is less than zero. -or- <see cref="Value"/> is greater than
        /// <c>DateTimeOffset.MaxValue.Ticks</c>.
        /// </exception>
        public DateTimeOffset AsDateTimeOffset() => DateTimeOffset.FromFileTime(Value);

        /// <summary>
        /// Converts the value of the current <see cref="WindowsFileTime"/> object to its equivalent string
        /// representation.
        /// </summary>
        /// <returns>
        /// A string representation of a <see cref="WindowsFileTime"/> object that includes the offset appended at the
        /// end of the string.
        /// </returns>
        public override string ToString() => AsDateTimeOffset().ToString();

        /// <summary>
        /// Converts the value of the current <see cref="WindowsFileTime"/> to its equivalent
        /// <see cref="DateTimeOffset"/> representation, with the offset set to the local time offset.
        /// </summary>
        /// <param name="data">A Windows file time, expressed in ticks.</param>
        /// <returns>
        /// An object that represents the date and time of <paramref name="data"/> with the offset set to the local time
        /// offset.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <see cref="Value"/> is less than zero. -or- <see cref="Value"/> is greater than
        /// <c>DateTimeOffset.MaxValue.Ticks</c>.
        /// </exception>
        public static implicit operator DateTimeOffset(WindowsFileTime data) => data.AsDateTimeOffset();
    }
}