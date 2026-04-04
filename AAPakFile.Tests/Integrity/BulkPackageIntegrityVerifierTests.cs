using AAPakFile.Chunking;
using AAPakFile.Core;
using AAPakFile.Editing;

using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Integrity;

public class BulkPackageIntegrityVerifierTests
{
    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pak");

    private static async Task<(string path, SafeFileHandle handle, IEnumerable<PackedFileRecord> records)>
        CreateTestPackageAsync(CancellationToken cancellationToken, params (string name, byte[] content)[] entries)
    {
        var path = NewTempPath();

        await using (var editor = await PackageEditor.CreateAsync(path,
                         cancellationToken: cancellationToken))
        {
            foreach (var (name, content) in entries)
                await editor.AddOrReplaceFileAsync(name, new ReadOnlyMemory<byte>(content),
                    cancellationToken: cancellationToken);
            await editor.SaveAsync(cancellationToken);
        }

        var handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
        var records = await FileTableHelper.LoadRecordsAsync(handle,
            cancellationToken: cancellationToken);
        return (path, handle, records);
    }

    [Test]
    public async Task VerifyAsync_AllFilesCorrect_ReturnsAllTrue(CancellationToken cancellationToken)
    {
        (string path, SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await CreateTestPackageAsync(
            cancellationToken,
            ("file1.txt", "Hello"u8.ToArray()),
            ("file2.txt", "World"u8.ToArray()));
        try
        {
            using (handle)
            {
                var verifier = new BulkPackageIntegrityVerifier(new ChunkedFileProcessor());
                var results = await verifier
                    .VerifyAsync(handle, records, cancellationToken).ToListAsync();

                await Assert.That(results).Count().IsEqualTo(2);
                await Assert.That(results.All(r => r.IsFileIntegrityIntact)).IsTrue();
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task VerifyAsync_EmptyFileList_ReturnsNoResults(CancellationToken cancellationToken)
    {
        (string path, SafeFileHandle handle, _) = await CreateTestPackageAsync(
            cancellationToken,
            ("placeholder.txt", "x"u8.ToArray()));
        try
        {
            using (handle)
            {
                var verifier = new BulkPackageIntegrityVerifier(new ChunkedFileProcessor());
                var results = await verifier.VerifyAsync(handle, [], cancellationToken)
                    .ToListAsync();

                await Assert.That(results).IsEmpty();
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task VerifyAsync_ReportsProcessedAndIntactCounts(CancellationToken cancellationToken)
    {
        (string path, SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await CreateTestPackageAsync(
            cancellationToken,
            ("a.bin", new byte[32]),
            ("b.bin", new byte[64]),
            ("c.bin", new byte[16]));
        try
        {
            using (handle)
            {
                var verifier = new BulkPackageIntegrityVerifier(new ChunkedFileProcessor());
                var results = await verifier
                    .VerifyAsync(handle, records, cancellationToken).ToListAsync();

                await Assert.That(results).Count().IsEqualTo(3);

                // The last result should reflect all 3 processed and all 3 intact.
                var last = results.Last();
                await Assert.That(last.ProcessedFilesCount).IsEqualTo(3);
                await Assert.That(last.IntactFilesCount).IsEqualTo(3);
                await Assert.That(last.TotalFilesCount).IsEqualTo(3);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task VerifyAsync_CancelledToken_ThrowsOperationCanceledException(CancellationToken cancellationToken)
    {
        (string path, SafeFileHandle handle, IEnumerable<PackedFileRecord> records) = await CreateTestPackageAsync(
            cancellationToken,
            ("cancel.bin", new byte[64]));
        try
        {
            using (handle)
            {
                using var cts = new CancellationTokenSource();
                await cts.CancelAsync();

                var verifier = new BulkPackageIntegrityVerifier(new ChunkedFileProcessor());
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await verifier.VerifyAsync(handle, records, cts.Token)
                        .ToListAsync(cancellationToken));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task VerifyAsync_CorruptedFile_ReportsIsFileIntegrityIntactFalse(CancellationToken cancellationToken)
    {
        var content = new byte[64]; // all zeros
        (string path, SafeFileHandle handle, _) = await CreateTestPackageAsync(cancellationToken, ("corrupt.bin", content));
        try
        {
            // Release the handle so we can write to the file
            handle.Dispose();

            // File data is at offset 0 in a freshly-created single-file package; flip the first byte
            await using (var fs = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(0x01);
            }

            using var corruptHandle = File.OpenHandle(path, options: FileOptions.Asynchronous);
            // Reload records from the corrupted package (header/FAT are intact; only data changed)
            var corruptRecords = await FileTableHelper.LoadRecordsAsync(corruptHandle,
                cancellationToken: cancellationToken);

            var verifier = new BulkPackageIntegrityVerifier(new ChunkedFileProcessor());
            var results = await verifier
                .VerifyAsync(corruptHandle, corruptRecords, cancellationToken)
                .ToListAsync();

            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0].IsFileIntegrityIntact).IsFalse();
            await Assert.That(results[0].IntactFilesCount).IsEqualTo(0);
        }
        finally
        {
            File.Delete(path);
        }
    }
}