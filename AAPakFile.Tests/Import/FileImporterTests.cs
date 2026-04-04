using AAPakFile.Editing;

namespace AAPakFile.Import;

public class FileImporterTests
{
    // Progress<T> dispatches via SynchronizationContext, which may not fire before assertions.
    // This implementation invokes the callback synchronously on the calling thread.
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    [Test]
    public async Task ImportAsync_ImportsAllFilesFromDirectory(CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "a.txt"), "aaa",
                cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "b.txt"), "bbb",
                cancellationToken);

            var editorMock = Mock.Of<IPackageEditor>();

            var importer = new FileImporter();
            await importer.ImportAsync(editorMock.Object, tempDir, cancellationToken: cancellationToken);

            editorMock.AddOrReplaceFileAsync(
                    Any<string>(), Any<Stream>(),
                    Any<PackageWriteOptions>(), Any<CancellationToken>())
                .WasCalled(Times.Exactly(2));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task ImportAsync_NormalizesBackslashesToForwardSlashes(CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(subDir, "file.txt"), "content",
                cancellationToken);

            var capturedNames = new List<string>();
            var editorMock = Mock.Of<IPackageEditor>();
            editorMock.AddOrReplaceFileAsync(
                    Any<string>(), Any<Stream>(),
                    Any<PackageWriteOptions>(), Any<CancellationToken>())
                .Callback(args => capturedNames.Add((string)args[0]!));

            var importer = new FileImporter();
            await importer.ImportAsync(editorMock.Object, tempDir,
                cancellationToken: cancellationToken);

            var name = await Assert.That(capturedNames).HasSingleItem();
            await Assert.That(name).DoesNotContain("\\");
            await Assert.That(name).Contains("sub/file.txt");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task ImportAsync_ReportsProgressPerFile(CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "1.txt"), "1",
                cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "2.txt"), "2",
                cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "3.txt"), "3",
                cancellationToken);

            var editorMock = Mock.Of<IPackageEditor>();

            var progressReports = new List<ImportProgress>();
            var progress = new SyncProgress<ImportProgress>(progressReports.Add);

            var importer = new FileImporter();
            await importer.ImportAsync(editorMock.Object, tempDir, progress,
                cancellationToken);

            await Assert.That(progressReports).Count().IsEqualTo(3);
            await Assert.That(progressReports.Last().ImportedFilesCount).IsEqualTo(3);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task ImportAsync_RespectsCancellationToken(CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            for (var i = 0; i < 5; i++)
                await File.WriteAllTextAsync(Path.Combine(tempDir, $"{i}.txt"), i.ToString(),
                    cancellationToken);

            var editorMock = Mock.Of<IPackageEditor>();

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var importer = new FileImporter();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await importer.ImportAsync(editorMock.Object, tempDir,
                    cancellationToken: cts.Token));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}