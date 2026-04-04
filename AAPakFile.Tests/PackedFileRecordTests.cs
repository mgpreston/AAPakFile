using System.Runtime.InteropServices;

namespace AAPakFile;

public class PackedFileRecordTests
{
    // ── FileNameBuffer ─────────────────────────────────────────────────────────

    [Test]
    public void FileNameBuffer_WriteTo_NonAsciiBytes_ThrowsInvalidDataException()
    {
        var buf = new PackedFileRecord.FileNameBuffer();
        // Write a non-ASCII continuation byte (0x80) into the first position — invalid UTF-8.
        Span<byte> bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref buf, 1));
        bytes[0] = 0x80;

        Assert.Throws<InvalidDataException>(() => _ = buf.ToString());
    }

    [Test]
    public async Task FileNameBuffer_Equals_SameName_ReturnsTrue()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("hello.txt");
        var b = PackedFileRecord.FileNameBuffer.FromString("hello.txt");
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test]
    public async Task FileNameBuffer_Equals_DifferentName_ReturnsFalse()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("hello.txt");
        var b = PackedFileRecord.FileNameBuffer.FromString("world.txt");
        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task FileNameBuffer_Equals_Object_SameName_ReturnsTrue()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("test.pak");
        object b = PackedFileRecord.FileNameBuffer.FromString("test.pak");
        await Assert.That(a.Equals(b)).IsTrue();
    }

    [Test]
    public async Task FileNameBuffer_Equals_Object_Null_ReturnsFalse()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("test.pak");
        await Assert.That(a.Equals(null)).IsFalse();
    }

    [Test]
    public async Task FileNameBuffer_Equals_Object_WrongType_ReturnsFalse()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("test.pak");
        // ReSharper disable once SuspiciousTypeConversion.Global
        await Assert.That(a.Equals("test.pak")).IsFalse();
    }

    [Test]
    public async Task FileNameBuffer_GetHashCode_SameNames_ReturnsSameHash()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("file.bin");
        var b = PackedFileRecord.FileNameBuffer.FromString("file.bin");
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task FileNameBuffer_EqualityOperator_SameName_ReturnsTrue()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("equal.txt");
        var b = PackedFileRecord.FileNameBuffer.FromString("equal.txt");
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task FileNameBuffer_InequalityOperator_DifferentNames_ReturnsTrue()
    {
        var a = PackedFileRecord.FileNameBuffer.FromString("left.txt");
        var b = PackedFileRecord.FileNameBuffer.FromString("right.txt");
        await Assert.That(a != b).IsTrue();
    }

    // ── Md5Buffer ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Md5Buffer_ToString_ReturnsHexString()
    {
        var buf = new PackedFileRecord.Md5Buffer();
        var str = buf.ToString();
        // All-zero Md5Buffer → 32 hex zeros
        await Assert.That(str).IsEqualTo(new string('0', 32));
        await Assert.That(str.Length).IsEqualTo(32);
    }

    // ── WindowsFileTime ────────────────────────────────────────────────────────

    [Test]
    public async Task WindowsFileTime_ToString_ReturnsNonEmptyString()
    {
        var wft = new PackedFileRecord.WindowsFileTime
        {
            Value = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero).ToFileTime()
        };
        var str = wft.ToString();
        await Assert.That(str).IsNotEmpty();
    }
}