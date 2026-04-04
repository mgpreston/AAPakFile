using System.IO.Compression;

using AAPakFile.Editing;

namespace AAPakFile.Import;

public class ZipImporterTests
{
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private static string CreateZipFile(Action<ZipArchive> populate)
    {
        var path = Path.GetTempFileName() + ".zip";
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        populate(zip);
        return path;
    }

    [Test]
    public async Task ImportAsync_ImportsFileEntries(CancellationToken cancellationToken)
    {
        var zipPath = CreateZipFile(zip =>
        {
            using (var s = zip.CreateEntry("file1.txt").Open()) s.Write("hello"u8);
            using (var s = zip.CreateEntry("file2.bin").Open()) s.Write("world"u8);
        });
        try
        {
            var editorMock = Mock.Of<IPackageEditor>();

            var importer = new ZipImporter();
            await importer.ImportAsync(editorMock.Object, zipPath,
                cancellationToken: cancellationToken);

            editorMock.AddOrReplaceFileAsync(
                    Any<string>(), Any<Stream>(),
                    Any<PackageWriteOptions>(), Any<CancellationToken>())
                .WasCalled(Times.Exactly(2));
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Test]
    public async Task ImportAsync_SkipsDirectoryEntries(CancellationToken cancellationToken)
    {
        var zipPath = CreateZipFile(zip =>
        {
            // A directory entry has a trailing '/'.
            zip.CreateEntry("subdir/");
            using var s = zip.CreateEntry("subdir/actual.txt").Open();
            s.Write("data"u8);
        });
        try
        {
            var editorMock = Mock.Of<IPackageEditor>();

            var importer = new ZipImporter();
            await importer.ImportAsync(editorMock.Object, zipPath,
                cancellationToken: cancellationToken);

            // Only the file entry, not the directory entry.
            editorMock.AddOrReplaceFileAsync(
                    Any<string>(), Any<Stream>(),
                    Any<PackageWriteOptions>(), Any<CancellationToken>())
                .WasCalled(Times.Once);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Test]
    public async Task ImportAsync_ReportsProgressWithTotalCount(CancellationToken cancellationToken)
    {
        var zipPath = CreateZipFile(zip =>
        {
            using (var s = zip.CreateEntry("a.txt").Open()) s.Write("a"u8);
            using (var s = zip.CreateEntry("b.txt").Open()) s.Write("b"u8);
        });
        try
        {
            var editorMock = Mock.Of<IPackageEditor>();

            var progressReports = new List<ImportProgress>();
            var progress = new SyncProgress<ImportProgress>(progressReports.Add);

            var importer = new ZipImporter();
            await importer.ImportAsync(editorMock.Object, zipPath, progress,
                cancellationToken: cancellationToken);

            await Assert.That(progressReports).Count().IsEqualTo(2);
            await Assert.That(progressReports[0].TotalFilesCount).IsEqualTo(2);
            await Assert.That(progressReports[1].ImportedFilesCount).IsEqualTo(2);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Test]
    public async Task ImportAsync_PassesSizeHintMatchingEntryLength(CancellationToken cancellationToken)
    {
        const string content = "size hint test content";
        var zipPath = CreateZipFile(zip =>
        {
            var entry = zip.CreateEntry("hinted.txt");
            using var stream = entry.Open();
            stream.Write(System.Text.Encoding.UTF8.GetBytes(content));
        });
        try
        {
            long? capturedSizeHint = null;
            var editorMock = Mock.Of<IPackageEditor>();
            editorMock.AddOrReplaceFileAsync(
                    Any<string>(), Any<Stream>(),
                    Any<PackageWriteOptions>(), Any<CancellationToken>())
                .Callback(args =>
                    capturedSizeHint = (args[2] as PackageWriteOptions)?.SizeHint);

            var importer = new ZipImporter();
            await importer.ImportAsync(editorMock.Object, zipPath,
                cancellationToken: cancellationToken);

            // The SizeHint should equal the uncompressed length from the ZIP directory.
            await Assert.That(capturedSizeHint).IsNotNull();
            await Assert.That(capturedSizeHint!.Value).IsEqualTo(
                System.Text.Encoding.UTF8.GetByteCount(content));
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Test]
    public async Task ImportAsync_RespectsCancellationToken()
    {
        var zipPath = CreateZipFile(zip =>
        {
            for (var i = 0; i < 5; i++)
            {
                using var s = zip.CreateEntry($"{i}.txt").Open();
                s.Write([(byte)('0' + i)]);
            }
        });
        try
        {
            var editorMock = Mock.Of<IPackageEditor>();

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var importer = new ZipImporter();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await importer.ImportAsync(editorMock.Object, zipPath,
                    cancellationToken: cts.Token));
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}