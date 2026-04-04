using System.IO.Compression;

using AAPakFile.Chunking;
using AAPakFile.Core;
using AAPakFile.Editing;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Export;

public class ZipExporterStateTests
{
    [Test]
    public async Task State_DisposeAsync_WithOpenStream_DisposesAndNullsOut(CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        var entry = zip.CreateEntry("state_test.txt");
        var entryStream = await entry.OpenAsync(cancellationToken);

        var state = new ZipExporter.State(zip, CompressionLevel.Fastest)
        {
            CurrentFileEntry = entry,
            CurrentFileEntryStream = entryStream
        };

        await state.DisposeAsync();

        await Assert.That(state.CurrentFileEntryStream).IsNull();
        await Assert.That(state.CurrentFileEntry).IsNull();
    }

    [Test]
    public async Task State_DisposeAsync_WithNullStream_DoesNotThrow()
    {
        using var ms = new MemoryStream();
        await using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

        var state = new ZipExporter.State(zip, CompressionLevel.Fastest);
        // CurrentFileEntryStream is null by default

        await state.DisposeAsync(); // should not throw
    }
}

public class ZipExporterTests
{
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private static string NewTempPath(string extension = ".pak") =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);

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
    public async Task ExportAsync_CreatesZipWithCorrectEntries(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var zipPath = NewTempPath(".zip");
        try
        {
            var content1 = "zip content one"u8.ToArray();
            var content2 = "zip content two"u8.ToArray();

            (SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await OpenTestPackageAsync(pkgPath,
                cancellationToken,
                ("one.txt", content1),
                ("two.txt", content2));
            using (handle)
            {
                var exporter = new ZipExporter(new ChunkedFileProcessor());
                await exporter.ExportAsync(handle, records, zipPath,
                    cancellationToken: cancellationToken);
            }

            await using var zipStream = File.OpenRead(zipPath);
            await using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

            await Assert.That(zip.Entries).Count().IsEqualTo(2);

            var entry1 = zip.Entries.FirstOrDefault(e => e.FullName == "one.txt");
            var entry2 = zip.Entries.FirstOrDefault(e => e.FullName == "two.txt");

            await Assert.That(entry1).IsNotNull();
            await Assert.That(entry2).IsNotNull();

            await using var s1 = await entry1!.OpenAsync(cancellationToken);
            var buf1 = new byte[content1.Length];
            _ = await s1.ReadAsync(buf1, cancellationToken);
            await Assert.That(buf1).IsSequenceEqualTo(content1);
        }
        finally
        {
            File.Delete(pkgPath);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Test]
    public async Task ExportAsync_ReportsProgress(CancellationToken cancellationToken)
    {
        var pkgPath = NewTempPath();
        var zipPath = NewTempPath(".zip");
        try
        {
            (SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await OpenTestPackageAsync(pkgPath,
                cancellationToken,
                ("x.bin", new byte[8]),
                ("y.bin", new byte[16]));
            using (handle)
            {
                var progressReports = new List<ExportProgress>();
                var progress = new SyncProgress<ExportProgress>(progressReports.Add);

                var exporter = new ZipExporter(new ChunkedFileProcessor());
                await exporter.ExportAsync(handle, records, zipPath, progress,
                    cancellationToken: cancellationToken);

                await Assert.That(progressReports).IsNotEmpty();
                await Assert.That(progressReports.Last().ExportedFilesCount).IsEqualTo(2);
            }
        }
        finally
        {
            File.Delete(pkgPath);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Test]
    public async Task ExportAsync_InvalidModifiedTime_SuppressesArgumentOutOfRangeException(CancellationToken cancellationToken)
    {
        // ModifiedTime.Value = 0 → Jan 1 1601 — before the ZIP minimum of 1980.
        // ZipArchiveEntry.LastWriteTime would throw ArgumentOutOfRangeException;
        // ZipExporter.ProcessChunkAsync catches and suppresses it.
        var pkgPath = NewTempPath();
        var zipPath = NewTempPath(".zip");
        try
        {
            (SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await OpenTestPackageAsync(pkgPath, cancellationToken, ("dated.txt", "data"u8.ToArray()));
            using (handle)
            {
                var oldRecords = records.Select(r => r with
                {
                    ModifiedTime = new PackedFileRecord.WindowsFileTime { Value = 0 }
                }).ToList();

                var exporter = new ZipExporter(new ChunkedFileProcessor());
                // Must not throw — the ArgumentOutOfRangeException is caught and suppressed.
                await exporter.ExportAsync(handle, oldRecords, zipPath,
                    cancellationToken: cancellationToken);
            }

            await using var zipStream = File.OpenRead(zipPath);
            await using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            await Assert.That(zip.Entries).Count().IsEqualTo(1);
        }
        finally
        {
            File.Delete(pkgPath);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }
}