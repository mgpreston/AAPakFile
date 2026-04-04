namespace AAPakFile;

/// <summary>
/// Contains the default AES-128 symmetric secret key used by XLGames for ArcheAge.
/// </summary>
public static class XlGamesAesKey
{
    /// <summary>
    /// The default AES-128 symmetric secret key used by XLGames for ArcheAge.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> Default = new byte[]
        { 0x32, 0x1F, 0x2A, 0xEE, 0xAA, 0x58, 0x4A, 0xB4, 0x9A, 0x6C, 0x9E, 0x09, 0xD5, 0x9E, 0x9C, 0x6F };
}