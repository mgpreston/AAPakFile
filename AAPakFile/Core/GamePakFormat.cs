using System.Buffers.Binary;

namespace AAPakFile.Core;

internal static class GamePakFormat
{
    internal static ReadOnlySpan<byte> Magic => "CryTek"u8;

    internal const uint FlagsModelManifest = 0xFFFF0000u;
    internal const uint FlagsGameAsset = 0xFFFF0001u;
    internal const uint ExpectedVersion = 1861u;
    internal const uint ExpectedHeaderSize = 20u;
    internal const int EntryBytes = 20;

    /// <summary>
    /// Parses one 20-byte entry record from the raw table at position <paramref name="i"/>.
    /// Returns (Attr, Field4, RelativeOffset, FieldA, SizeField).
    /// </summary>
    internal static (ushort Attr, uint Field4, uint RelativeOffset, uint FieldA, uint SizeField)
        ParseEntry(ReadOnlySpan<byte> table, int i)
    {
        var o = i * EntryBytes;
        return (
            BinaryPrimitives.ReadUInt16LittleEndian(table[o..]),
            BinaryPrimitives.ReadUInt32LittleEndian(table[(o + 4)..]),
            BinaryPrimitives.ReadUInt32LittleEndian(table[(o + 8)..]),
            BinaryPrimitives.ReadUInt32LittleEndian(table[(o + 12)..]),
            BinaryPrimitives.ReadUInt32LittleEndian(table[(o + 16)..])
        );
    }

    /// <summary>
    /// Validates a 24-byte archive header and returns (flags, fileCount).
    /// Throws <see cref="FormatException"/> if the header is not a valid CryTek archive header.
    /// </summary>
    internal static (uint Flags, uint FileCount) ParseHeader(ReadOnlySpan<byte> header)
    {
        if (!header[..6].SequenceEqual(Magic))
            throw new FormatException("Not a CryTek archive (invalid magic).");

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        var version = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        var hdrSz = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);

        if (version != ExpectedVersion || hdrSz != ExpectedHeaderSize || count == 0)
            throw new FormatException(
                $"Unsupported CryTek header (version={version}, hdrSz={hdrSz}, count={count}).");

        if (flags != FlagsModelManifest && flags != FlagsGameAsset)
            throw new FormatException($"Unknown CryTek archive flags: 0x{flags:X8}.");

        return (flags, count);
    }
}