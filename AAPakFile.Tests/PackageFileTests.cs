using System.IO.Compression;

using AAPakFile.Editing;
using AAPakFile.Integrity;

namespace AAPakFile;

public class PackageFileTests
{
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private static string NewTempPath(string ext = ".pak") =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ext);

    private static async Task CreatePackageWithFilesAsync(string packagePath,
        CancellationToken cancellationToken,
        params (string name, byte[] content)[] entries)
    {
        await using var editor = await PackageEditor.CreateAsync(packagePath,
            cancellationToken: cancellationToken);
        foreach ((string name, byte[] content) in entries)
            await editor.AddOrReplaceFileAsync(name, new ReadOnlyMemory<byte>(content),
                cancellationToken: cancellationToken);
        await editor.SaveAsync(cancellationToken);
    }

    [Test]
    public async Task CreateEditorAsync_CreatesNewEmptyPackage(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageFile.CreateEditorAsync(path,
                cancellationToken: cancellationToken);
            await Assert.That(editor.Entries).IsEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task OpenEditorAsync_OpensExistingPackage(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await CreatePackageWithFilesAsync(path, cancellationToken, ("hello.txt", "hello"u8.ToArray()));
            await using var editor = await PackageFile.OpenEditorAsync(path,
                cancellationToken: cancellationToken);
            await Assert.That(editor.Entries).Count().IsEqualTo(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ExportToFolderAsync_ExportsFiles(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var outDir = NewTempPath("");
        Directory.CreateDirectory(outDir);
        try
        {
            var content = "export to folder"u8.ToArray();
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken, ("output.txt", content));

            await PackageFile.ExportToFolderAsync(pkgPath, outDir,
                cancellationToken: cancellationToken);

            var exported = Path.Combine(outDir, "output.txt");
            await Assert.That(File.Exists(exported)).IsTrue();
            await Assert.That(await File.ReadAllBytesAsync(exported, cancellationToken)).IsSequenceEqualTo(content);
        }
        finally
        {
            File.Delete(pkgPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }

    [Test]
    public async Task ExportToZipArchiveAsync_ExportsFilesToZip(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var zipPath = NewTempPath(".zip");
        try
        {
            var content = "zip export"u8.ToArray();
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken, ("zipped.txt", content));

            await PackageFile.ExportToZipArchiveAsync(pkgPath, zipPath,
                cancellationToken: cancellationToken);

            await using var fs = File.OpenRead(zipPath);
            await using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            await Assert.That(zip.Entries).Count().IsEqualTo(1);
            await Assert.That(zip.Entries[0].FullName).IsEqualTo("zipped.txt");
        }
        finally
        {
            File.Delete(pkgPath);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Test]
    public async Task ImportFromFolderAsync_ImportsFilesIntoPackage(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var srcDir = NewTempPath("");
        Directory.CreateDirectory(srcDir);
        try
        {
            // ImportAllFromFolderAsync opens an existing package, so create one first
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(srcDir, "imported.txt"), "imported content",
                cancellationToken);

            await PackageFile.ImportFromFolderAsync(pkgPath, srcDir,
                cancellationToken: cancellationToken);

            await using var editor = await PackageEditor.OpenAsync(pkgPath,
                cancellationToken: cancellationToken);
            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("imported.txt");
        }
        finally
        {
            File.Delete(pkgPath);
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, recursive: true);
        }
    }

    [Test]
    public async Task ImportFromZipArchiveAsync_ImportsFilesIntoPackage(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var zipPath = NewTempPath(".zip");
        try
        {
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken);

            await using (var fs = File.Create(zipPath))
            await using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                await using var s = await zip.CreateEntry("from_zip.txt")
                    .OpenAsync(cancellationToken);
                s.Write("from zip"u8);
            }

            await PackageFile.ImportFromZipArchiveAsync(pkgPath, zipPath,
                cancellationToken: cancellationToken);

            await using var editor = await PackageEditor.OpenAsync(pkgPath,
                cancellationToken: cancellationToken);
            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("from_zip.txt");
        }
        finally
        {
            File.Delete(pkgPath);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Test]
    public async Task VerifyAllFilesAsync_ValidPackage_ReturnsAllIntact(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        try
        {
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken,
                ("a.txt", "aaa"u8.ToArray()),
                ("b.txt", "bbb"u8.ToArray()));

            var results = await PackageFile
                .VerifyAllFilesAsync(pkgPath, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);

            await Assert.That(results).Count().IsEqualTo(2);
            await Assert.That(results.All(r => r.IsFileIntegrityIntact)).IsTrue();
        }
        finally
        {
            File.Delete(pkgPath);
        }
    }

    [Test]
    public async Task VerifyPackageAsync_AllValid_NullProgress_ReturnsSuccess(CancellationToken cancellationToken)
    {
        // Verifies the null-progress branch: progress?.Report(...) is a no-op when progress is null.
        var pkgPath = NewTempPath();
        try
        {
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken, ("ok.txt", "valid"u8.ToArray()));
            var result = await PackageFile.VerifyPackageAsync(pkgPath,
                cancellationToken: cancellationToken); // progress = null (default)
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.InvalidRecord).IsNull();
        }
        finally
        {
            File.Delete(pkgPath);
        }
    }

    [Test]
    public async Task VerifyPackageAsync_AllValid_ReturnsSuccessAndReportsProgress(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        try
        {
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken, ("ok.txt", "valid data"u8.ToArray()));

            var progressReports = new List<VerifyProgress>();
            var progress = new SyncProgress<VerifyProgress>(p => progressReports.Add(p));
            var result = await PackageFile.VerifyPackageAsync(pkgPath, progress: progress,
                cancellationToken: cancellationToken);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.InvalidRecord).IsNull();
            await Assert.That(progressReports).IsNotEmpty();
            await Assert.That(progressReports.Last().ProcessedFilesCount).IsEqualTo(1);
            await Assert.That(progressReports.Last().ValidFilesCount).IsEqualTo(1);
            await Assert.That(progressReports.Last().TotalFilesCount).IsEqualTo(1);
        }
        finally
        {
            File.Delete(pkgPath);
        }
    }

    [Test]
    public async Task OpenReaderAsync_WhenCancelled_DisposesHandleAndThrows(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        try
        {
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken, ("x.txt", "x"u8.ToArray()));
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await PackageFile.OpenReaderAsync(pkgPath, cancellationToken: cts.Token));
        }
        finally
        {
            File.Delete(pkgPath);
        }
    }

    [Test]
    public async Task CompactAsync_WhenCancelled_CleansUpAndThrows(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        try
        {
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken, ("x.txt", "x"u8.ToArray()));
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await PackageFile.CompactAsync(pkgPath, cancellationToken: cts.Token));
        }
        finally
        {
            File.Delete(pkgPath);
        }
    }

    [Test]
    public async Task VerifyPackageAsync_CorruptedFile_ReturnsFailure(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        try
        {
            // File data is written starting at offset 0 in a freshly-created package
            var content = new byte[64];
            await CreatePackageWithFilesAsync(pkgPath, cancellationToken, ("corrupt.txt", content));

            // Flip the first byte of the file data to invalidate the MD5
            await using (var fs = File.Open(pkgPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(0x01);
            }

            var result = await PackageFile.VerifyPackageAsync(pkgPath,
                cancellationToken: cancellationToken);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.InvalidRecord).IsNotNull();
        }
        finally
        {
            File.Delete(pkgPath);
        }
    }
}