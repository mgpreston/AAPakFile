using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace AAPakFile.Reading;

/// <summary>
/// Tests for <see cref="ZipPackageReader"/> and the ZIP-backed path through
/// <see cref="PackageReaderEntry"/>.
/// </summary>
[Category("Integration")]
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class ZipPackageReaderTests
{
    // ── Fixture data ──────────────────────────────────────────────────────────

    // Created once in [Before(Class)], shared across all test instances.
    private static string s_emptyZipPath = null!;
    private static string s_singleFileZipPath = null!;
    private static string s_multiFileZipPath = null!;
    private static string s_dirAndFileZipPath = null!;

    private static readonly byte[] HelloContent = "Hello, World!"u8.ToArray();
    private static readonly byte[] ReadmeContent = "This is a readme."u8.ToArray();
    private static readonly byte[] ValuesContent = [1, 2, 3, 4, 5];

    // A DateTimeOffset that ZIP's MS-DOS timestamp format can represent exactly:
    // aligned to a 2-second boundary, no sub-second component.
    private static readonly DateTimeOffset FixedTime =
        new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    // LastWriteTime value as actually stored and read back from the ZIP archive,
    // which may differ from FixedTime due to MS-DOS timestamp precision/timezone.
    private static DateTimeOffset s_storedFixedTime;

    [Before(Class)]
    public static void CreateFixtures()
    {
        s_emptyZipPath = CreateZipFile(_ => { });
        s_singleFileZipPath = CreateZipFile(CreateSingleFileZip);
        s_multiFileZipPath = CreateZipFile(CreateMultiFileZip);
        s_dirAndFileZipPath = CreateZipFile(CreateDirAndFileZip);

        // Capture the timestamp exactly as ZIP round-trips it, to avoid precision surprises.
        using var fs = new FileStream(s_singleFileZipPath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
        s_storedFixedTime = archive.GetEntry("hello.txt")!.LastWriteTime;
    }

    [After(Class)]
    public static void DeleteFixtures()
    {
        File.Delete(s_emptyZipPath);
        File.Delete(s_singleFileZipPath);
        File.Delete(s_multiFileZipPath);
        File.Delete(s_dirAndFileZipPath);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateZipFile(Action<ZipArchive> populate)
    {
        var path = Path.GetTempFileName() + ".zip";
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        populate(zip);
        return path;
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] content,
        DateTimeOffset? lastWriteTime = null)
    {
        var entry = zip.CreateEntry(name);
        if (lastWriteTime.HasValue) entry.LastWriteTime = lastWriteTime.Value;
        using var s = entry.Open();
        s.Write(content);
    }

    private static void CreateSingleFileZip(ZipArchive zip)
        => AddEntry(zip, "hello.txt", HelloContent, FixedTime);

    private static void CreateMultiFileZip(ZipArchive zip)
    {
        AddEntry(zip, "readme.txt", ReadmeContent);
        AddEntry(zip, "data/values.bin", ValuesContent);
    }

    private static void CreateDirAndFileZip(ZipArchive zip)
    {
        zip.CreateEntry("subdir/");                       // directory entry — trailing '/'
        AddEntry(zip, "subdir/actual.txt", HelloContent);
    }

    // ── OpenAsync: entry count and filtering ──────────────────────────────────

    [Test]
    public async Task OpenAsync_EmptyZip_HasNoEntries(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_emptyZipPath, cancellationToken);
        await Assert.That(reader.Entries).IsEmpty();
    }

    [Test]
    public async Task OpenAsync_SingleFileZip_HasOneEntry(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await Assert.That(reader.Entries).Count().IsEqualTo(1);
    }

    [Test]
    public async Task OpenAsync_MultiFileZip_HasCorrectEntryCount(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_multiFileZipPath, cancellationToken);
        await Assert.That(reader.Entries).Count().IsEqualTo(2);
    }

    [Test]
    public async Task OpenAsync_DirectoryEntries_AreExcluded(CancellationToken cancellationToken)
    {
        // ZIP has one directory entry ("subdir/") and one file entry ("subdir/actual.txt").
        // Only the file entry should appear.
        await using var reader = await ZipPackageReader.OpenAsync(s_dirAndFileZipPath, cancellationToken);
        await Assert.That(reader.Entries).Count().IsEqualTo(1);
        await Assert.That(reader.Entries[0].Name).IsEqualTo("subdir/actual.txt");
    }

    [Test]
    public async Task OpenAsync_MultiFileZip_EntryNamesAreCorrect(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_multiFileZipPath, cancellationToken);
        var names = reader.Entries.Select(e => e.Name).ToList();

        await Assert.That(names).Contains("readme.txt");
        await Assert.That(names).Contains("data/values.bin");
    }

    // ── PackageReaderEntry: metadata ─────────────────────────────────────────

    [Test]
    public async Task Entry_Name_MatchesZipEntryFullName(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await Assert.That(reader.Entries[0].Name).IsEqualTo("hello.txt");
    }

    [Test]
    public async Task Entry_FileSize_MatchesUncompressedLength(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await Assert.That(reader.Entries[0].FileSize).IsEqualTo(HelloContent.Length);
    }

    [Test]
    public async Task Entry_CreationTime_EqualsStoredLastWriteTime(CancellationToken cancellationToken)
    {
        // ZIP only has one timestamp; both CreationTime and ModifiedTime come from LastWriteTime.
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await Assert.That(reader.Entries[0].CreationTime).IsEqualTo(s_storedFixedTime);
    }

    [Test]
    public async Task Entry_ModifiedTime_EqualsStoredLastWriteTime(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await Assert.That(reader.Entries[0].ModifiedTime).IsEqualTo(s_storedFixedTime);
    }

    [Test]
    public async Task Entry_CreationTime_EqualsModifiedTime(CancellationToken cancellationToken)
    {
        // Since ZIP stores a single timestamp, both fields must be identical.
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        var entry = reader.Entries[0];
        await Assert.That(entry.CreationTime).IsEqualTo(entry.ModifiedTime);
    }

    [Test]
    public async Task Entry_Md5Hash_IsEmpty(CancellationToken cancellationToken)
    {
        // ZIP archives do not store MD5 hashes.
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await Assert.That(reader.Entries[0].Md5Hash.IsEmpty).IsTrue();
    }

    // ── OpenRead: stream capabilities ────────────────────────────────────────

    [Test]
    public async Task OpenRead_Stream_CanRead(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    public async Task OpenRead_Stream_CannotSeek(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        await Assert.That(stream.CanSeek).IsFalse();
    }

    [Test]
    public async Task OpenRead_Stream_CannotWrite(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        await Assert.That(stream.CanWrite).IsFalse();
    }

    // ── OpenRead: unsupported operations ─────────────────────────────────────

    [Test]
    public async Task OpenRead_Length_ThrowsNotSupportedException(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        Assert.Throws<NotSupportedException>(() => _ = stream.Length);
    }

    [Test]
    public async Task OpenRead_PositionGet_ThrowsNotSupportedException(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        Assert.Throws<NotSupportedException>(() => _ = stream.Position);
    }

    [Test]
    public async Task OpenRead_PositionSet_ThrowsNotSupportedException(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }

    [Test]
    public async Task OpenRead_Seek_ThrowsNotSupportedException(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Test]
    public async Task OpenRead_SetLength_ThrowsNotSupportedException(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Test]
    public async Task OpenRead_Write_ThrowsNotSupportedException(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        Assert.Throws<NotSupportedException>(() => stream.Write([0], 0, 1));
    }

    [Test]
    public async Task OpenRead_Flush_DoesNotThrow(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        await stream.FlushAsync(cancellationToken); // must not throw
    }

    // ── OpenRead: reading content ─────────────────────────────────────────────

    [Test]
    public async Task OpenRead_ReadByteArray_ContentMatchesOriginal(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();

        var buf = new byte[HelloContent.Length];
        var read = await stream.ReadAsync(buf, cancellationToken);

        await Assert.That(read).IsEqualTo(HelloContent.Length);
        await Assert.That(buf).IsSequenceEqualTo(HelloContent);
    }

    [Test]
    public async Task OpenRead_ReadSpan_ContentMatchesOriginal(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();

        var buf = new byte[HelloContent.Length];
        var read = stream.Read(buf.AsSpan());

        await Assert.That(read).IsEqualTo(HelloContent.Length);
        await Assert.That(buf).IsSequenceEqualTo(HelloContent);
    }

    [Test]
    public async Task OpenRead_ReadAsyncByteArray_ContentMatchesOriginal(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();

        var buf = new byte[HelloContent.Length];
        var read = await stream.ReadAsync(buf, cancellationToken);

        await Assert.That(read).IsEqualTo(HelloContent.Length);
        await Assert.That(buf).IsSequenceEqualTo(HelloContent);
    }

    [Test]
    public async Task OpenRead_ReadAsyncMemory_ContentMatchesOriginal(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();

        var buf = new byte[HelloContent.Length];
        var read = await stream.ReadAsync(buf.AsMemory(), cancellationToken);

        await Assert.That(read).IsEqualTo(HelloContent.Length);
        await Assert.That(buf).IsSequenceEqualTo(HelloContent);
    }

    [Test]
    public async Task OpenRead_MultipleEntries_ContentMatchesOriginal(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_multiFileZipPath, cancellationToken);

        foreach (var entry in reader.Entries)
        {
            await using var stream = entry.OpenRead();
            var buf = new byte[entry.FileSize];
            await stream.ReadExactlyAsync(buf, cancellationToken);

            var expected = entry.Name switch
            {
                "readme.txt" => ReadmeContent,
                "data/values.bin" => ValuesContent,
                _ => throw new InvalidOperationException($"Unexpected entry: {entry.Name}")
            };
            await Assert.That(buf).IsSequenceEqualTo(expected);
        }
    }

    // ── OpenRead: dispose chain ───────────────────────────────────────────────

    [Test]
    public async Task OpenRead_StreamDispose_DoesNotThrow(CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        var stream = reader.Entries[0].OpenRead();
        await stream.DisposeAsync(); // must release FileStream + ZipArchive + entryStream without throwing
    }

    [Test]
    public async Task OpenRead_StreamDispose_SubsequentReadThrowsObjectDisposedException(
        CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        var stream = reader.Entries[0].OpenRead();
        await stream.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
    }

    // ── OpenRead: concurrent access ───────────────────────────────────────────

    [Test]
    public async Task OpenRead_ConcurrentCallsOnDifferentEntries_AllSucceed(
        CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_multiFileZipPath, cancellationToken);

        // Each call opens its own FileStream + ZipArchive — they must not interfere.
        var tasks = reader.Entries.Select(async entry =>
        {
            await using var stream = entry.OpenRead();
            var buf = new byte[entry.FileSize];
            await stream.ReadExactlyAsync(buf, cancellationToken);
            return buf;
        });

        var results = await Task.WhenAll(tasks);
        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results.All(b => b.Length > 0)).IsTrue();
    }

    [Test]
    public async Task OpenRead_CalledTwiceOnSameEntry_ReturnsTwoIndependentStreams(
        CancellationToken cancellationToken)
    {
        await using var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        var entry = reader.Entries[0];

        await using var stream1 = entry.OpenRead();
        await using var stream2 = entry.OpenRead();

        var buf1 = new byte[HelloContent.Length];
        var buf2 = new byte[HelloContent.Length];
        await stream1.ReadExactlyAsync(buf1, cancellationToken);
        await stream2.ReadExactlyAsync(buf2, cancellationToken);

        await Assert.That(buf1).IsSequenceEqualTo(HelloContent);
        await Assert.That(buf2).IsSequenceEqualTo(HelloContent);
    }

    // ── OpenRead: entry removed between OpenAsync and OpenRead ────────────────

    [Test]
    public async Task OpenRead_EntryRemovedFromZip_ThrowsFileNotFoundException(
        CancellationToken cancellationToken)
    {
        // ZipPackageReader holds no file handle after OpenAsync, so the ZIP can be replaced.
        var zipPath = CreateZipFile(zip => AddEntry(zip, "gone.txt", HelloContent));
        try
        {
            await using var reader = await ZipPackageReader.OpenAsync(zipPath, cancellationToken);

            // Overwrite the ZIP at the same path with one that no longer contains "gone.txt".
            await using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            await using (new ZipArchive(fs, ZipArchiveMode.Create))
            { /* empty — no entries */ }

            Assert.Throws<FileNotFoundException>(() => _ = reader.Entries[0].OpenRead());
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    // ── DisposeAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task DisposeAsync_IsNoOp_DoesNotThrow(CancellationToken cancellationToken)
    {
        var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await reader.DisposeAsync(); // should complete immediately without error
    }

    [Test]
    public async Task DisposeAsync_WhileStreamStillOpen_DoesNotAffectStream(
        CancellationToken cancellationToken)
    {
        // Because ZipPackageReader holds no persistent handle, disposing it while a stream
        // obtained from OpenRead() is still live must not invalidate that stream.
        var reader = await ZipPackageReader.OpenAsync(s_singleFileZipPath, cancellationToken);
        await using var stream = reader.Entries[0].OpenRead();
        await reader.DisposeAsync();

        var buf = new byte[HelloContent.Length];
        var read = await stream.ReadAsync(buf.AsMemory(), cancellationToken);

        await Assert.That(read).IsEqualTo(HelloContent.Length);
        await Assert.That(buf).IsSequenceEqualTo(HelloContent);
    }

    // ── PackageFile facade ────────────────────────────────────────────────────

    [Test]
    public async Task OpenZipReaderAsync_ViaPackageFile_ReturnsFunctionalReader(
        CancellationToken cancellationToken)
    {
        await using var reader = await PackageFile.OpenZipReaderAsync(
            s_singleFileZipPath, cancellationToken);

        await Assert.That(reader.Entries).Count().IsEqualTo(1);
        await Assert.That(reader.Entries[0].Name).IsEqualTo("hello.txt");
    }
}