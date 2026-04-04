using AAPakFile.Chunking;
using AAPakFile.Core;
using AAPakFile.Editing;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Export;

public class FileExporterTests
{
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private static string NewTempPath(string extension = ".pak") =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);

    /// <summary>
    /// Creates a package at <paramref name="pkgPath"/> containing the given file entries,
    /// then returns the file records sorted by offset (ready to pass to an exporter).
    /// </summary>
    private static async Task<(SafeFileHandle handle, IEnumerable<PackedFileRecord> records)>
        OpenTestPackageAsync(string pkgPath, CancellationToken cancellationToken, params (string name, byte[] content)[] entries)
    {
        await using (var editor = await PackageEditor.CreateAsync(pkgPath,
                         cancellationToken: cancellationToken))
        {
            foreach ((string name, byte[] content) in entries)
                await editor.AddOrReplaceFileAsync(name, new ReadOnlyMemory<byte>(content),
                    cancellationToken: cancellationToken);
            await editor.SaveAsync(cancellationToken);
        }

        var handle = File.OpenHandle(pkgPath, options: FileOptions.Asynchronous);
        var records = await FileTableHelper.LoadRecordsAsync(handle,
            cancellationToken: cancellationToken);
        return (handle, records);
    }

    [Test]
    public async Task ExportAsync_ExportsFileContentsToOutputDir(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var outputDir = NewTempPath("");
        Directory.CreateDirectory(outputDir);
        try
        {
            var content = "exported content"u8.ToArray();
            (SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await OpenTestPackageAsync(pkgPath, cancellationToken, ("hello.txt", content));
            using (handle)
            {
                var exporter = new FileExporter(new ChunkedFileProcessor());
                await exporter.ExportAsync(handle, records, outputDir,
                    cancellationToken: cancellationToken);
            }

            var exportedPath = Path.Combine(outputDir, "hello.txt");
            await Assert.That(File.Exists(exportedPath)).IsTrue();
            await Assert.That(await File.ReadAllBytesAsync(exportedPath)).IsSequenceEqualTo(content);
        }
        finally
        {
            File.Delete(pkgPath);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [Test]
    public async Task ExportAsync_CreatesSubdirectoriesForNestedPaths(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var outputDir = NewTempPath("");
        Directory.CreateDirectory(outputDir);
        try
        {
            var content = "nested"u8.ToArray();
            (SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await OpenTestPackageAsync(pkgPath, cancellationToken, ("subdir/nested.bin", content));
            using (handle)
            {
                var exporter = new FileExporter(new ChunkedFileProcessor());
                await exporter.ExportAsync(handle, records, outputDir,
                    cancellationToken: cancellationToken);
            }

            var exportedPath = Path.Combine(outputDir, "subdir", "nested.bin");
            await Assert.That(File.Exists(exportedPath)).IsTrue();
        }
        finally
        {
            File.Delete(pkgPath);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [Test]
    public async Task ExportAsync_ReportsProgress(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var outputDir = NewTempPath("");
        Directory.CreateDirectory(outputDir);
        try
        {
            (SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await OpenTestPackageAsync(pkgPath,
                cancellationToken,
                ("a.bin", new byte[10]),
                ("b.bin", new byte[20]));
            using (handle)
            {
                var progressReports = new List<ExportProgress>();
                var progress = new SyncProgress<ExportProgress>(p => progressReports.Add(p));

                var exporter = new FileExporter(new ChunkedFileProcessor());
                await exporter.ExportAsync(handle, records, outputDir, progress,
                    cancellationToken);

                await Assert.That(progressReports).IsNotEmpty();
                await Assert.That(progressReports.Last().ExportedFilesCount).IsEqualTo(2);
            }
        }
        finally
        {
            File.Delete(pkgPath);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [Test]
    public async Task State_Dispose_WithOpenHandle_DisposesHandleAndNullsProperty()
    {
        // Exercises the non-null branch of FileExporter.State.Dispose — the path taken when
        // Dispose is called while CurrentFileHandle is still open (e.g. due to an error mid-export).
        var tmpFile = Path.GetTempFileName();
        try
        {
            var handle = File.OpenHandle(tmpFile);
            var state = new FileExporter.State(Path.GetTempPath())
            {
                CurrentFileHandle = handle
            };

            state.Dispose();

            await Assert.That(state.CurrentFileHandle).IsNull();
            await Assert.That(handle.IsClosed).IsTrue();
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}