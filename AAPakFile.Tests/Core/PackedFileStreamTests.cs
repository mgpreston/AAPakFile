using System.Diagnostics.CodeAnalysis;

namespace AAPakFile.Core;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class PackedFileStreamTests
{
    private static readonly byte[] TestFileContent =
        Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    // Creates a temp file with TestFileContent and returns its path.
    private static string CreateTempFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, TestFileContent);
        return path;
    }

    [Test]
    public async Task CanRead_ReturnsTrue()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            await Assert.That(stream.CanRead).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task CanSeek_ReturnsTrue()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            await Assert.That(stream.CanSeek).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task CanWrite_ReturnsFalse()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            await Assert.That(stream.CanWrite).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Length_ReturnsFileLength()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 10, fileLength: 50);

            await Assert.That(stream.Length).IsEqualTo(50L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Position_InitiallyZero()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            await Assert.That(stream.Position).IsEqualTo(0L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Read_ArrayOverload_ReadsCorrectBytes()
    {
        var path = CreateTempFile();
        try
        {
            // Expose bytes [10..19] of the test file.
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 10, fileLength: 10);

            var buffer = new byte[10];
            var read = stream.Read(buffer, 0, 10);

            await Assert.That(read).IsEqualTo(10);
            await Assert.That(buffer).IsSequenceEqualTo(TestFileContent[10..20]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Read_SpanOverload_ReadsCorrectBytes()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 20, fileLength: 15);

            var bufferArr = new byte[15];
            var read = stream.Read(bufferArr.AsSpan());

            await Assert.That(read).IsEqualTo(15);
            await Assert.That(bufferArr).IsSequenceEqualTo(TestFileContent[20..35]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Read_WhenAtEnd_ReturnsZero()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            stream.Seek(0, SeekOrigin.End); // Position = fileLength
            var buffer = new byte[10];
            var read = stream.Read(buffer, 0, 10);

            await Assert.That(read).IsEqualTo(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Seek_BeginOrigin_SetsAbsolutePosition()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 100);

            var pos = stream.Seek(42, SeekOrigin.Begin);

            await Assert.That(pos).IsEqualTo(42L);
            await Assert.That(stream.Position).IsEqualTo(42L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Seek_CurrentOrigin_OffsetFromCurrentPosition()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 100);

            stream.Seek(10, SeekOrigin.Begin);
            var pos = stream.Seek(5, SeekOrigin.Current);

            await Assert.That(pos).IsEqualTo(15L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Seek_EndOrigin_OffsetFromEnd()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 100);

            var pos = stream.Seek(-10, SeekOrigin.End);

            await Assert.That(pos).IsEqualTo(90L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Seek_ClampsToZero_WhenResultNegative()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 100);

            var pos = stream.Seek(-999, SeekOrigin.Begin);

            await Assert.That(pos).IsEqualTo(0L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Seek_ClampsToLength_WhenResultExceedsLength()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 100);

            var pos = stream.Seek(999, SeekOrigin.Begin);

            await Assert.That(pos).IsEqualTo(100L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Seek_InvalidOrigin_ThrowsArgumentOutOfRangeException()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 100);

            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)99));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Position_Setter_MovesPosition()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            await using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 100);

            stream.Position = 55;

            await Assert.That(stream.Position).IsEqualTo(55L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Flush_DoesNotThrow()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            // Should not throw.
            stream.Flush();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void SetLength_ThrowsNotSupportedException()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            Assert.Throws<NotSupportedException>(() => stream.SetLength(5));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Write_ThrowsNotSupportedException()
    {
        var path = CreateTempFile();
        try
        {
            using var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            using var stream = new PackedFileStream(handle, fileOffset: 0, fileLength: 10);

            Assert.Throws<NotSupportedException>(() => stream.Write([1, 2, 3], 0, 3));
        }
        finally
        {
            File.Delete(path);
        }
    }
}