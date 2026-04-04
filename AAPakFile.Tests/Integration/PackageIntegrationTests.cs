using System.IO.Compression;

using AAPakFile.Core;
using AAPakFile.Editing;
using AAPakFile.Reading;

namespace AAPakFile.Integration;

/// <summary>
/// End-to-end tests that exercise the full read/write pipeline using fixture packages
/// with known, deterministic content.
/// </summary>
/// <remarks>
/// Run only integration tests:
///   dotnet run --project AAPakFile.Tests -- --filter Category=Integration
/// Exclude integration tests (unit tests only):
///   dotnet run --project AAPakFile.Tests -- --filter Category!=Integration
/// </remarks>
[Category("Integration")]
public sealed class PackageIntegrationTests
{
    // Set once in [Before(Class)], shared across all test instances.
    private static string s_emptyPakPath = null!;
    private static string s_singleFilePakPath = null!;
    private static string s_multiFilePakPath = null!;

    [Before(Class)]
    public static async Task CreateFixturesAsync()
    {
        s_emptyPakPath = Path.Combine(Path.GetTempPath(), $"int_empty_{Guid.NewGuid():N}.pak");
        s_singleFilePakPath = Path.Combine(Path.GetTempPath(), $"int_single_{Guid.NewGuid():N}.pak");
        s_multiFilePakPath = Path.Combine(Path.GetTempPath(), $"int_multi_{Guid.NewGuid():N}.pak");

        await IntegrationFixtures.CreateEmptyAsync(s_emptyPakPath);
        await IntegrationFixtures.CreateSingleFileAsync(s_singleFilePakPath);
        await IntegrationFixtures.CreateMultiFileAsync(s_multiFilePakPath);
    }

    [After(Class)]
    public static void DeleteFixtures()
    {
        File.Delete(s_emptyPakPath);
        File.Delete(s_singleFilePakPath);
        File.Delete(s_multiFilePakPath);
    }

    // ── Empty package ─────────────────────────────────────────────────────────────

    [Test]
    public async Task EmptyPackage_LoadRecords_HasNoEntries(CancellationToken cancellationToken)
    {
        using var handle = File.OpenHandle(s_emptyPakPath, options: FileOptions.Asynchronous);
        var records = await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken);
        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task EmptyPackage_IntegrityVerification_Succeeds(CancellationToken cancellationToken)
    {
        var result = await PackageFile.VerifyPackageAsync(s_emptyPakPath, cancellationToken: cancellationToken);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.InvalidRecord).IsNull();
    }

    // ── Single-file package ───────────────────────────────────────────────────────

    [Test]
    public async Task SingleFilePackage_LoadRecords_HasCorrectEntryMetadata(CancellationToken cancellationToken)
    {
        using var handle = File.OpenHandle(s_singleFilePakPath, options: FileOptions.Asynchronous);
        var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();

        await Assert.That(records).Count().IsEqualTo(1);

        var r = records[0];
        await Assert.That(r.FileName.ToString()).IsEqualTo("hello.txt");
        await Assert.That(r.FileSize).IsEqualTo(IntegrationFixtures.HelloContent.Length);
        // Timestamps survive the serialisation round-trip as FILETIME ticks.
        await Assert.That(r.CreationTime.Value).IsEqualTo(IntegrationFixtures.FixedTime.ToFileTime());
        await Assert.That(r.ModifiedTime.Value).IsEqualTo(IntegrationFixtures.FixedTime.ToFileTime());
    }

    [Test]
    public async Task SingleFilePackage_ReadStream_ContentMatchesFixture(CancellationToken cancellationToken)
    {
        using var handle = File.OpenHandle(s_singleFilePakPath, options: FileOptions.Asynchronous);
        var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
        var r = records[0];

        await using var stream = new PackedFileStream(handle, r.FileOffset, r.FileSize);
        var bytes = new byte[IntegrationFixtures.HelloContent.Length];
        var read = await stream.ReadAsync(bytes, cancellationToken);

        await Assert.That(read).IsEqualTo(IntegrationFixtures.HelloContent.Length);
        await Assert.That(bytes).IsSequenceEqualTo(IntegrationFixtures.HelloContent);
    }

    [Test]
    public async Task SingleFilePackage_ExportToFolder_ContentMatchesFixture(CancellationToken cancellationToken)
    {
        var outDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(outDir);
        try
        {
            await PackageFile.ExportToFolderAsync(s_singleFilePakPath, outDir, cancellationToken: cancellationToken);

            var exported = await File.ReadAllBytesAsync(Path.Combine(outDir, "hello.txt"), cancellationToken);
            await Assert.That(exported).IsSequenceEqualTo(IntegrationFixtures.HelloContent);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Test]
    public async Task SingleFilePackage_ExportToZip_ProducesCorrectArchive(CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
        try
        {
            await PackageFile.ExportToZipArchiveAsync(s_singleFilePakPath, zipPath, cancellationToken: cancellationToken);

            await using var fs = File.OpenRead(zipPath);
            await using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            await Assert.That(zip.Entries).Count().IsEqualTo(1);
            await Assert.That(zip.Entries[0].FullName).IsEqualTo("hello.txt");

            await using var entryStream = await zip.Entries[0].OpenAsync(cancellationToken);
            var bytes = new byte[IntegrationFixtures.HelloContent.Length];
            _ = await entryStream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(IntegrationFixtures.HelloContent);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Test]
    public async Task SingleFilePackage_IntegrityVerification_Succeeds(CancellationToken cancellationToken)
    {
        var result = await PackageFile.VerifyPackageAsync(s_singleFilePakPath, cancellationToken: cancellationToken);
        await Assert.That(result.Success).IsTrue();
    }

    // ── Multi-file package ────────────────────────────────────────────────────────

    [Test]
    public async Task MultiFilePackage_LoadRecords_HasCorrectEntryNames(CancellationToken cancellationToken)
    {
        using var handle = File.OpenHandle(s_multiFilePakPath, options: FileOptions.Asynchronous);
        var records = await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken);
        var names = records.Select(r => r.FileName.ToString()).ToList();

        await Assert.That(names).Count().IsEqualTo(3);
        await Assert.That(names).Contains("readme.txt");
        await Assert.That(names).Contains("data/values.bin");
        await Assert.That(names).Contains("data/config.txt");
    }

    [Test]
    public async Task MultiFilePackage_ExportToFolder_AllContentsMatchFixtures(CancellationToken cancellationToken)
    {
        var outDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(outDir);
        try
        {
            await PackageFile.ExportToFolderAsync(s_multiFilePakPath, outDir, cancellationToken: cancellationToken);

            await Assert.That(await File.ReadAllBytesAsync(Path.Combine(outDir, "readme.txt"), cancellationToken))
                .IsSequenceEqualTo(IntegrationFixtures.ReadmeContent);
            await Assert.That(await File.ReadAllBytesAsync(Path.Combine(outDir, "data", "values.bin"), cancellationToken))
                .IsSequenceEqualTo(IntegrationFixtures.ValuesContent);
            await Assert.That(await File.ReadAllBytesAsync(Path.Combine(outDir, "data", "config.txt"), cancellationToken))
                .IsSequenceEqualTo(IntegrationFixtures.ConfigContent);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Test]
    public async Task MultiFilePackage_IntegrityVerification_AllFilesIntact(CancellationToken cancellationToken)
    {
        var results = await PackageFile
            .VerifyAllFilesAsync(s_multiFilePakPath, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken: cancellationToken);

        await Assert.That(results).Count().IsEqualTo(3);
        await Assert.That(results.All(r => r.IsFileIntegrityIntact)).IsTrue();
    }

    // ── Edit round-trips (each test works on a private copy of the fixture) ───────

    [Test]
    public async Task EditRoundTrip_AddFile_NewEntryIsReadableAfterReopen(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            var newContent = "integration added"u8.ToArray();
            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                await editor.AddOrReplaceFileAsync("added.txt", new ReadOnlyMemory<byte>(newContent), cancellationToken: cancellationToken);
                await editor.SaveAsync(cancellationToken);
            }

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(4);

            var addedRecord = records.Single(r => r.FileName.ToString() == "added.txt");
            await using var stream = new PackedFileStream(handle, addedRecord.FileOffset, addedRecord.FileSize);
            var bytes = new byte[newContent.Length];
            _ = await stream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(newContent);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task EditRoundTrip_ReplaceFile_UpdatedContentIsReadableAfterReopen(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_singleFilePakPath);
        try
        {
            var replacement = "replaced content"u8.ToArray();
            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                await editor.AddOrReplaceFileAsync("hello.txt", new ReadOnlyMemory<byte>(replacement), cancellationToken: cancellationToken);
                await editor.SaveAsync(cancellationToken);
            }

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(1);

            var r = records[0];
            await using var stream = new PackedFileStream(handle, r.FileOffset, r.FileSize);
            var bytes = new byte[replacement.Length];
            _ = await stream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(replacement);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task EditRoundTrip_DeleteFile_EntryAbsentAfterReopen(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                editor.DeleteFile("readme.txt");
                await editor.SaveAsync(cancellationToken);
            }

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();

            await Assert.That(records).Count().IsEqualTo(2);
            await Assert.That(records.Any(r => r.FileName.ToString() == "readme.txt")).IsFalse();
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task EditRoundTrip_IntegrityVerificationPassesAfterEdit(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                await editor.AddOrReplaceFileAsync("extra.txt",
                    new ReadOnlyMemory<byte>("extra"u8.ToArray()), cancellationToken: cancellationToken);
                await editor.SaveAsync(cancellationToken);
            }

            var result = await PackageFile.VerifyPackageAsync(copy, cancellationToken: cancellationToken);
            await Assert.That(result.Success).IsTrue();
        }
        finally
        {
            File.Delete(copy);
        }
    }

    // ── PackageReader ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Reader_EmptyPackage_HasNoEntries(CancellationToken cancellationToken)
    {
        await using var reader = await PackageReader.OpenAsync(s_emptyPakPath, cancellationToken: cancellationToken);
        await Assert.That(reader.Entries).IsEmpty();
    }

    [Test]
    public async Task Reader_SingleFilePackage_EntryHasCorrectMetadata(CancellationToken cancellationToken)
    {
        await using var reader = await PackageReader.OpenAsync(s_singleFilePakPath, cancellationToken: cancellationToken);

        await Assert.That(reader.Entries).Count().IsEqualTo(1);

        var entry = reader.Entries[0];
        await Assert.That(entry.Name).IsEqualTo("hello.txt");
        await Assert.That(entry.FileSize).IsEqualTo(IntegrationFixtures.HelloContent.Length);
        await Assert.That(entry.CreationTime.ToFileTime()).IsEqualTo(IntegrationFixtures.FixedTime.ToFileTime());
        await Assert.That(entry.ModifiedTime.ToFileTime()).IsEqualTo(IntegrationFixtures.FixedTime.ToFileTime());
    }

    [Test]
    public async Task Reader_SingleFilePackage_EntryMd5HashMatchesRecordMd5(CancellationToken cancellationToken)
    {
        // Load the record directly and materialise its Md5 bytes (local variable — no defensive-copy concern).
        using var handle = File.OpenHandle(s_singleFilePakPath, options: FileOptions.Asynchronous);
        var record = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).Single();
        var expected = record.Md5.AsSpan().ToArray();

        await using var reader = await PackageReader.OpenAsync(s_singleFilePakPath, cancellationToken: cancellationToken);
        await Assert.That(reader.Entries[0].Md5Hash.ToArray()).IsSequenceEqualTo(expected);
    }

    [Test]
    public async Task Reader_SingleFilePackage_OpenRead_ContentMatchesFixture(CancellationToken cancellationToken)
    {
        await using var reader = await PackageReader.OpenAsync(s_singleFilePakPath, cancellationToken: cancellationToken);
        var entry = reader.Entries[0];

        await using var stream = entry.OpenRead();
        var bytes = new byte[IntegrationFixtures.HelloContent.Length];
        var read = await stream.ReadAsync(bytes, cancellationToken);

        await Assert.That(read).IsEqualTo(IntegrationFixtures.HelloContent.Length);
        await Assert.That(bytes).IsSequenceEqualTo(IntegrationFixtures.HelloContent);
    }

    [Test]
    public async Task Reader_MultiFilePackage_HasCorrectEntryNames(CancellationToken cancellationToken)
    {
        await using var reader = await PackageReader.OpenAsync(s_multiFilePakPath, cancellationToken: cancellationToken);
        var names = reader.Entries.Select(e => e.Name).ToList();

        await Assert.That(names).Count().IsEqualTo(3);
        await Assert.That(names).Contains("readme.txt");
        await Assert.That(names).Contains("data/values.bin");
        await Assert.That(names).Contains("data/config.txt");
    }

    [Test]
    public async Task Reader_ConcurrentReads_AllSucceed(CancellationToken cancellationToken)
    {
        await using var reader1 = await PackageReader.OpenAsync(s_multiFilePakPath, cancellationToken: cancellationToken);
        await using var reader2 = await PackageReader.OpenAsync(s_multiFilePakPath, cancellationToken: cancellationToken);

        await Assert.That(reader1.Entries).Count().IsEqualTo(3);
        await Assert.That(reader2.Entries).Count().IsEqualTo(3);

        // Read the same entries concurrently from both readers and verify content matches.
        var tasks = reader1.Entries.Zip(reader2.Entries, async (e1, e2) =>
        {
            await using var s1 = e1.OpenRead();
            await using var s2 = e2.OpenRead();
            var b1 = new byte[e1.FileSize];
            var b2 = new byte[e2.FileSize];
            await s1.ReadExactlyAsync(b1, cancellationToken);
            await s2.ReadExactlyAsync(b2, cancellationToken);
            return b1.SequenceEqual(b2);
        });
        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(r => r)).IsTrue();
    }

    [Test]
    public async Task Reader_OpenViaPackageFile_ReturnsFunctionalReader(CancellationToken cancellationToken)
    {
        await using var reader = await PackageFile.OpenReaderAsync(s_singleFilePakPath, cancellationToken: cancellationToken);

        await Assert.That(reader.Entries).Count().IsEqualTo(1);
        await Assert.That(reader.Entries[0].Name).IsEqualTo("hello.txt");
    }

    // ── RenameFile ────────────────────────────────────────────────────────────────

    [Test]
    public async Task RenameFile_NewNameReadableAfterReopen(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_singleFilePakPath);
        try
        {
            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                editor.RenameFile("hello.txt", "renamed.txt");
                await editor.SaveAsync(cancellationToken);
            }

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(1);
            await Assert.That(records[0].FileName.ToString()).IsEqualTo("renamed.txt");

            await using var stream = new PackedFileStream(handle, records[0].FileOffset, records[0].FileSize);
            var bytes = new byte[IntegrationFixtures.HelloContent.Length];
            _ = await stream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(IntegrationFixtures.HelloContent);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task RenameFile_OldNameAbsentAfterReopen(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_singleFilePakPath);
        try
        {
            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                editor.RenameFile("hello.txt", "renamed.txt");
                await editor.SaveAsync(cancellationToken);
            }

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken);
            await Assert.That(records.Any(r => r.FileName.ToString() == "hello.txt")).IsFalse();
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task RenameFile_ContentUnchanged(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_singleFilePakPath);
        try
        {
            long fileSizeBefore;
            byte[] md5Before;
            using (var handleBefore = File.OpenHandle(copy, options: FileOptions.Asynchronous))
            {
                var record = (await FileTableHelper.LoadRecordsAsync(handleBefore, cancellationToken: cancellationToken)).Single();
                fileSizeBefore = record.FileSize;
                md5Before = record.Md5.AsSpan().ToArray();
            }

            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                editor.RenameFile("hello.txt", "renamed.txt");
                await editor.SaveAsync(cancellationToken);
            }

            using var handleAfter = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var renamed = (await FileTableHelper.LoadRecordsAsync(handleAfter, cancellationToken: cancellationToken)).Single();
            await Assert.That(renamed.FileSize).IsEqualTo(fileSizeBefore);
            await Assert.That(renamed.Md5.AsSpan().ToArray()).IsSequenceEqualTo(md5Before);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    // ── Create from source ────────────────────────────────────────────────────────

    [Test]
    public async Task CreateFromFolder_ProducesPackageWithAllFiles(CancellationToken cancellationToken)
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var pakPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pak");
        Directory.CreateDirectory(sourceDir);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(sourceDir, "alpha.bin"), IntegrationFixtures.ValuesContent, cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(sourceDir, "beta.txt"), IntegrationFixtures.HelloContent, cancellationToken);

            await PackageFile.CreateFromFolderAsync(pakPath, sourceDir, cancellationToken: cancellationToken);

            using var handle = File.OpenHandle(pakPath, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(2);

            var alpha = records.Single(r => r.FileName.ToString() == "alpha.bin");
            await using var alphaStream = new PackedFileStream(handle, alpha.FileOffset, alpha.FileSize);
            var alphaBytes = new byte[alpha.FileSize];
            _ = await alphaStream.ReadAsync(alphaBytes, cancellationToken);
            await Assert.That(alphaBytes).IsSequenceEqualTo(IntegrationFixtures.ValuesContent);
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
            File.Delete(pakPath);
        }
    }

    [Test]
    public async Task CreateFromZipArchive_ProducesPackageWithAllFiles(CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
        var pakPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pak");
        try
        {
            await using (var zip = await ZipFile.OpenAsync(zipPath, ZipArchiveMode.Create, cancellationToken))
            {
                var e1 = zip.CreateEntry("gamma.txt");
                await using (var s = await e1.OpenAsync(cancellationToken)) await s.WriteAsync(IntegrationFixtures.ReadmeContent, cancellationToken);
                var e2 = zip.CreateEntry("data/delta.bin");
                await using (var s = await e2.OpenAsync(cancellationToken)) await s.WriteAsync(IntegrationFixtures.ValuesContent, cancellationToken);
            }

            await PackageFile.CreateFromZipArchiveAsync(pakPath, zipPath, cancellationToken: cancellationToken);

            using var handle = File.OpenHandle(pakPath, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(2);

            var names = records.Select(r => r.FileName.ToString()).ToList();
            await Assert.That(names).Contains("gamma.txt");
            await Assert.That(names).Contains("data/delta.bin");
        }
        finally
        {
            File.Delete(zipPath);
            File.Delete(pakPath);
        }
    }

    [Test]
    public async Task CreateFromFolder_OverwritesExistingPackage(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_singleFilePakPath);
        var sourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(sourceDir);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(sourceDir, "new.txt"), IntegrationFixtures.ConfigContent, cancellationToken);

            await PackageFile.CreateFromFolderAsync(copy, sourceDir, cancellationToken: cancellationToken);

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(1);
            await Assert.That(records[0].FileName.ToString()).IsEqualTo("new.txt");
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
            File.Delete(copy);
        }
    }

    // ── OpenFileAsync ─────────────────────────────────────────────────────────────

    [Test]
    public async Task OpenFile_ExistingFile_ContentMatchesFixture(CancellationToken cancellationToken)
    {
        await using var stream = await PackageFile.OpenFileAsync(s_singleFilePakPath, "hello.txt",
            cancellationToken: cancellationToken);
        var bytes = new byte[IntegrationFixtures.HelloContent.Length];
        var read = await stream.ReadAsync(bytes, cancellationToken);

        await Assert.That(read).IsEqualTo(IntegrationFixtures.HelloContent.Length);
        await Assert.That(bytes).IsSequenceEqualTo(IntegrationFixtures.HelloContent);
    }

    [Test]
    public async Task OpenFile_MissingFile_ThrowsFileNotFoundException(CancellationToken cancellationToken)
    {
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await PackageFile.OpenFileAsync(s_singleFilePakPath, "does_not_exist.txt",
                cancellationToken: cancellationToken));
    }

    [Test]
    public async Task OpenFile_DisposingStream_ReleasesHandle(CancellationToken cancellationToken)
    {
        var stream = await PackageFile.OpenFileAsync(s_singleFilePakPath, "hello.txt",
            cancellationToken: cancellationToken);
        await stream.DisposeAsync();

        // If the handle was released, a second open on the same file succeeds.
        await using var stream2 = await PackageFile.OpenFileAsync(s_singleFilePakPath, "hello.txt",
            cancellationToken: cancellationToken);
        await Assert.That(stream2.Length).IsEqualTo(IntegrationFixtures.HelloContent.Length);
    }

    // ── Filtered Export ───────────────────────────────────────────────────────────

    [Test]
    public async Task FilteredExport_ToFolder_OnlyExportsMatchingFiles(CancellationToken cancellationToken)
    {
        var outDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(outDir);
        try
        {
            await PackageFile.ExportToFolderAsync(s_multiFilePakPath, outDir,
                filter: r => ((string)r.FileName).StartsWith("data/"),
                cancellationToken: cancellationToken);

            await Assert.That(await File.ReadAllBytesAsync(Path.Combine(outDir, "data", "values.bin"), cancellationToken))
                .IsSequenceEqualTo(IntegrationFixtures.ValuesContent);
            await Assert.That(await File.ReadAllBytesAsync(Path.Combine(outDir, "data", "config.txt"), cancellationToken))
                .IsSequenceEqualTo(IntegrationFixtures.ConfigContent);
            await Assert.That(File.Exists(Path.Combine(outDir, "readme.txt"))).IsFalse();
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Test]
    public async Task FilteredExport_ToZip_OnlyExportsMatchingFiles(CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
        try
        {
            await PackageFile.ExportToZipArchiveAsync(s_multiFilePakPath, zipPath,
                filter: r => ((string)r.FileName).StartsWith("data/"),
                cancellationToken: cancellationToken);

            await using var fs = File.OpenRead(zipPath);
            await using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            await Assert.That(zip.Entries).Count().IsEqualTo(2);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            await Assert.That(names).Contains("data/values.bin");
            await Assert.That(names).Contains("data/config.txt");
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Test]
    public async Task FilteredExport_NoMatch_ExportsNothing(CancellationToken cancellationToken)
    {
        var outDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(outDir);
        try
        {
            await PackageFile.ExportToFolderAsync(s_multiFilePakPath, outDir,
                filter: _ => false,
                cancellationToken: cancellationToken);

            await Assert.That(Directory.GetFiles(outDir, "*", SearchOption.AllDirectories)).IsEmpty();
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    // ── CompactAsync ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Compact_PackageWithDeletedFiles_ReducesFileSize(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            var sizeBefore = new FileInfo(copy).Length;

            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                editor.DeleteFile("readme.txt");
                editor.DeleteFile("data/values.bin");
                await editor.SaveAsync(cancellationToken);
            }

            await PackageFile.CompactAsync(copy, cancellationToken: cancellationToken);

            var sizeAfter = new FileInfo(copy).Length;
            await Assert.That(sizeAfter).IsLessThan(sizeBefore);

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(1);
            await Assert.That(records[0].FileName.ToString()).IsEqualTo("data/config.txt");

            await using var stream = new PackedFileStream(handle, records[0].FileOffset, records[0].FileSize);
            var bytes = new byte[IntegrationFixtures.ConfigContent.Length];
            _ = await stream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(IntegrationFixtures.ConfigContent);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task CompactInPlace_PackageWithDeletedFiles_ReducesFileSize(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            var sizeBefore = new FileInfo(copy).Length;

            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                editor.DeleteFile("readme.txt");
                editor.DeleteFile("data/values.bin");
                await editor.SaveAsync(cancellationToken);
            }

            await PackageFile.CompactInPlaceAsync(copy, cancellationToken: cancellationToken);

            var sizeAfter = new FileInfo(copy).Length;
            await Assert.That(sizeAfter).IsLessThan(sizeBefore);

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(1);
            await Assert.That(records[0].FileName.ToString()).IsEqualTo("data/config.txt");

            await using var stream = new PackedFileStream(handle, records[0].FileOffset, records[0].FileSize);
            var bytes = new byte[IntegrationFixtures.ConfigContent.Length];
            _ = await stream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(IntegrationFixtures.ConfigContent);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task Compact_PackageWithReplacedFile_ReducesFileSize(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_singleFilePakPath);
        try
        {
            // Replace with much larger content so the old slot becomes an unused gap.
            var largeContent = new byte[64 * 1024];
            Random.Shared.NextBytes(largeContent);

            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                await editor.AddOrReplaceFileAsync("hello.txt", new ReadOnlyMemory<byte>(largeContent), cancellationToken: cancellationToken);
                await editor.SaveAsync(cancellationToken);
            }
            long sizeAfterReplace = new FileInfo(copy).Length;

            await PackageFile.CompactAsync(copy, cancellationToken: cancellationToken);

            var sizeAfterCompact = new FileInfo(copy).Length;
            await Assert.That(sizeAfterCompact).IsLessThan(sizeAfterReplace);

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(1);

            await using var stream = new PackedFileStream(handle, records[0].FileOffset, records[0].FileSize);
            var bytes = new byte[largeContent.Length];
            _ = await stream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(largeContent);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task CompactInPlace_PackageWithReplacedFile_ReducesFileSize(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_singleFilePakPath);
        try
        {
            var largeContent = new byte[64 * 1024];
            Random.Shared.NextBytes(largeContent);

            await using (var editor = await PackageEditor.OpenAsync(copy, cancellationToken: cancellationToken))
            {
                await editor.AddOrReplaceFileAsync("hello.txt", new ReadOnlyMemory<byte>(largeContent), cancellationToken: cancellationToken);
                await editor.SaveAsync(cancellationToken);
            }
            long sizeAfterReplace = new FileInfo(copy).Length;

            await PackageFile.CompactInPlaceAsync(copy, cancellationToken: cancellationToken);

            var sizeAfterCompact = new FileInfo(copy).Length;
            await Assert.That(sizeAfterCompact).IsLessThan(sizeAfterReplace);

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = (await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken)).ToList();
            await Assert.That(records).Count().IsEqualTo(1);

            await using var stream = new PackedFileStream(handle, records[0].FileOffset, records[0].FileSize);
            var bytes = new byte[largeContent.Length];
            _ = await stream.ReadAsync(bytes, cancellationToken);
            await Assert.That(bytes).IsSequenceEqualTo(largeContent);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task Compact_PreservesAllMetadata(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            using var handleBefore = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var before = (await FileTableHelper.LoadRecordsAsync(handleBefore, cancellationToken: cancellationToken))
                .OrderBy(r => r.FileName.ToString()).ToList();
            // ReSharper disable once DisposeOnUsingVariable
            handleBefore.Dispose();

            await PackageFile.CompactAsync(copy, cancellationToken: cancellationToken);

            using var handleAfter = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var after = (await FileTableHelper.LoadRecordsAsync(handleAfter, cancellationToken: cancellationToken))
                .OrderBy(r => r.FileName.ToString()).ToList();

            await Assert.That(after).Count().IsEqualTo(before.Count);
            for (var i = 0; i < before.Count; i++)
            {
                await Assert.That(after[i].FileName.ToString()).IsEqualTo(before[i].FileName.ToString());
                await Assert.That(after[i].FileSize).IsEqualTo(before[i].FileSize);
                await Assert.That(after[i].CreationTime.Value).IsEqualTo(before[i].CreationTime.Value);
                await Assert.That(after[i].ModifiedTime.Value).IsEqualTo(before[i].ModifiedTime.Value);
            }
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task CompactInPlace_PreservesAllMetadata(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            using var handleBefore = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var before = (await FileTableHelper.LoadRecordsAsync(handleBefore, cancellationToken: cancellationToken))
                .OrderBy(r => r.FileName.ToString()).ToList();
            // ReSharper disable once DisposeOnUsingVariable
            handleBefore.Dispose();

            await PackageFile.CompactInPlaceAsync(copy, cancellationToken: cancellationToken);

            using var handleAfter = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var after = (await FileTableHelper.LoadRecordsAsync(handleAfter, cancellationToken: cancellationToken))
                .OrderBy(r => r.FileName.ToString()).ToList();

            await Assert.That(after).Count().IsEqualTo(before.Count);
            for (var i = 0; i < before.Count; i++)
            {
                await Assert.That(after[i].FileName.ToString()).IsEqualTo(before[i].FileName.ToString());
                await Assert.That(after[i].FileSize).IsEqualTo(before[i].FileSize);
                await Assert.That(after[i].CreationTime.Value).IsEqualTo(before[i].CreationTime.Value);
                await Assert.That(after[i].ModifiedTime.Value).IsEqualTo(before[i].ModifiedTime.Value);
            }
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task Compact_EmptyPackage_Succeeds(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_emptyPakPath);
        try
        {
            await PackageFile.CompactAsync(copy, cancellationToken: cancellationToken);

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken);
            await Assert.That(records).IsEmpty();
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task CompactInPlace_EmptyPackage_Succeeds(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_emptyPakPath);
        try
        {
            await PackageFile.CompactInPlaceAsync(copy, cancellationToken: cancellationToken);

            using var handle = File.OpenHandle(copy, options: FileOptions.Asynchronous);
            var records = await FileTableHelper.LoadRecordsAsync(handle, cancellationToken: cancellationToken);
            await Assert.That(records).IsEmpty();
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task Compact_ReportsProgress(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            var reports = new List<Compaction.CompactProgress>();
            await PackageFile.CompactAsync(copy,
                progress: new SyncProgress<Compaction.CompactProgress>(reports.Add),
                cancellationToken: cancellationToken);

            await Assert.That(reports).IsNotEmpty();
            await Assert.That(reports.Last().ProcessedFilesCount).IsEqualTo(reports.Last().TotalFilesCount);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task CompactInPlace_ReportsProgress(CancellationToken cancellationToken)
    {
        var copy = CopyFixture(s_multiFilePakPath);
        try
        {
            var reports = new List<Compaction.CompactProgress>();
            await PackageFile.CompactInPlaceAsync(copy,
                progress: new SyncProgress<Compaction.CompactProgress>(reports.Add),
                cancellationToken: cancellationToken);

            await Assert.That(reports).IsNotEmpty();
            await Assert.That(reports.Last().ProcessedFilesCount).IsEqualTo(reports.Last().TotalFilesCount);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private static string CopyFixture(string source)
    {
        var dest = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pak");
        File.Copy(source, dest);
        return dest;
    }
}