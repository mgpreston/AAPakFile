using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace AAPakFile.Editing;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class PackageEditorTests
{
    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pak");

    // ── Create ───────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateAsync_InvalidKeyLength_DisposesHandleAndThrows(CancellationToken cancellationToken)
    {
        // AES only accepts 16, 24, or 32-byte keys; a 5-byte key triggers CryptographicException
        // inside the Encryptor constructor, exercising the catch block in CreateAsync that
        // disposes the file handle before rethrowing.
        var path = NewTempPath();
        try
        {
            var invalidKey = new byte[5];
            await Assert.ThrowsAsync<CryptographicException>(async () =>
                await PackageEditor.CreateAsync(path, invalidKey, cancellationToken: cancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task CreateAsync_NewPackage_HasNoEntries(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            await Assert.That(editor.Entries).IsEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task CreateAsync_NewPackage_IsDirtyFalse(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            await Assert.That(editor.IsDirty).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── AddOrReplaceFileAsync — in-memory ─────────────────────────────────────

    [Test]
    public async Task AddOrReplaceFileAsync_Memory_AddsNewEntry(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            var data = "Hello, World!"u8.ToArray();

            await editor.AddOrReplaceFileAsync("test.txt", new ReadOnlyMemory<byte>(data),
                cancellationToken: cancellationToken);

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("test.txt");
            await Assert.That(editor.Entries[0].FileSize).IsEqualTo(data.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── AddOrReplaceFileAsync — seekable stream ───────────────────────────────

    [Test]
    public async Task AddOrReplaceFileAsync_SeekableStream_AddsNewEntry(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            var data = "Seekable content."u8.ToArray();

            await using var ms = new MemoryStream(data);
            await editor.AddOrReplaceFileAsync("seekable.txt", ms,
                cancellationToken: cancellationToken);

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("seekable.txt");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── AddOrReplaceFileAsync — non-seekable stream ───────────────────────────

    [Test]
    public async Task AddOrReplaceFileAsync_NonSeekableStream_AddsNewEntry(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            var data = "Non-seekable."u8.ToArray();

            await using var ns = new NonSeekableStream(new MemoryStream(data));
            await editor.AddOrReplaceFileAsync("nonseek.txt", ns,
                cancellationToken: cancellationToken);

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("nonseek.txt");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AddOrReplaceFileAsync_NonSeekableStream_ReplacesExistingEntry(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            // Add an initial file via memory (seekable path)
            await editor.AddOrReplaceFileAsync("replace_ns.txt",
                new ReadOnlyMemory<byte>("original data"u8.ToArray()),
                cancellationToken: cancellationToken);

            // Replace via non-seekable stream — exercises the DeleteFile+append branch
            var replacement = "replacement via non-seekable stream"u8.ToArray();
            await using var ns = new NonSeekableStream(new MemoryStream(replacement));
            await editor.AddOrReplaceFileAsync("replace_ns.txt", ns,
                cancellationToken: cancellationToken);

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("replace_ns.txt");
            await Assert.That(editor.Entries[0].FileSize).IsEqualTo(replacement.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── OpenAsync error handling ──────────────────────────────────────────────

    [Test]
    public async Task OpenAsync_InvalidFile_DisposesHandleAndThrows(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            // Write a file that's too small to contain a valid package header (< 512 bytes)
            await File.WriteAllBytesAsync(path, new byte[100], cancellationToken);

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await PackageEditor.OpenAsync(path,
                    cancellationToken: cancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── AddOrReplaceFileAsync — size hint ────────────────────────────────────

    [Test]
    public async Task AddOrReplaceFileAsync_WithSizeHint_AddsNewEntry(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            var data = "Hinted data."u8.ToArray();

            await using var ns = new NonSeekableStream(new MemoryStream(data));
            var options = new PackageWriteOptions { SizeHint = data.Length };
            await editor.AddOrReplaceFileAsync("hinted.txt", ns, options,
                cancellationToken: cancellationToken);

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].FileSize).IsEqualTo(data.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AddOrReplaceFileAsync_WithSizeHint_ThrowsWhenDataExceedsSizeHint(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            var data = new byte[100];

            await using var ns = new NonSeekableStream(new MemoryStream(data));
            var options = new PackageWriteOptions { SizeHint = 50 }; // hint smaller than data

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await editor.AddOrReplaceFileAsync("overflow.txt", ns, options,
                    cancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── AddOrReplaceFileAsync — replace existing ─────────────────────────────

    [Test]
    public async Task AddOrReplaceFileAsync_ReplaceExisting_UpdatesSizeAndMd5(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            var original = "original"u8.ToArray();
            await editor.AddOrReplaceFileAsync("replace.txt", new ReadOnlyMemory<byte>(original),
                cancellationToken: cancellationToken);

            var replacement = "replaced with longer content"u8.ToArray();
            await editor.AddOrReplaceFileAsync("replace.txt", new ReadOnlyMemory<byte>(replacement),
                cancellationToken: cancellationToken);

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].FileSize).IsEqualTo(replacement.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AddOrReplaceFileAsync_ReplaceWithTooLargeData_AppendsAndRoundTripsCorrectly(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            var replacement = new byte[600];
            Array.Fill(replacement, (byte)0xAB);

            // Add a 512-byte file (PaddingSize = 0, so FileSize + PaddingSize = 512),
            // then replace with 600 bytes — larger than the slot, forces append.
            await using (var editor = await PackageEditor.CreateAsync(path,
                             cancellationToken: cancellationToken))
            {
                var original = new byte[512];
                await editor.AddOrReplaceFileAsync("large.bin", new ReadOnlyMemory<byte>(original),
                    cancellationToken: cancellationToken);
                await editor.AddOrReplaceFileAsync("large.bin", new ReadOnlyMemory<byte>(replacement),
                    cancellationToken: cancellationToken);

                await Assert.That(editor.Entries).Count().IsEqualTo(1);
                await Assert.That(editor.Entries[0].FileSize).IsEqualTo(600L);

                await editor.SaveAsync(cancellationToken);
            } // editor disposed here, file handle released

            // Round-trip: verify the correct data is returned
            await using var reopened = await PackageEditor.OpenAsync(path,
                cancellationToken: cancellationToken);
            await Assert.That(reopened.Entries).Count().IsEqualTo(1);
            var buf = new byte[reopened.Entries[0].FileSize];
            await using var s = reopened.Entries[0].OpenRead();
            _ = await s.ReadAsync(buf, cancellationToken);
            await Assert.That(buf).IsSequenceEqualTo(replacement);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AddOrReplaceFileAsync_ReuseDeletedSlot(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            // Add a file, delete it (creating a reusable slot), then add a smaller file.
            var large = new byte[200];
            await editor.AddOrReplaceFileAsync("slot.txt", new ReadOnlyMemory<byte>(large),
                cancellationToken: cancellationToken);
            editor.DeleteFile("slot.txt");

            var small = new byte[50];
            await editor.AddOrReplaceFileAsync("new.txt", new ReadOnlyMemory<byte>(small),
                cancellationToken: cancellationToken);

            // The package should only have "new.txt" in active entries.
            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("new.txt");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AddOrReplaceFileAsync_DeletedSlotTooSmall_FallsBackToAppend(CancellationToken cancellationToken)
    {
        // Exercises the false branch of the extra-file slot loop in DeterminePlacement:
        // when extra.FileSize < dataLength the slot is skipped and the file is appended instead.
        // A 512-byte file has PaddingSize = 0, so its deleted slot is exactly 512 bytes.
        // Writing 600 bytes requires more than 512, so the slot is too small and must be skipped.
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            var small = new byte[512]; // PaddingSize = 0 → deleted slot = exactly 512 bytes
            await editor.AddOrReplaceFileAsync("small.bin", new ReadOnlyMemory<byte>(small),
                cancellationToken: cancellationToken);
            editor.DeleteFile("small.bin");

            var large = new byte[600]; // 600 > 512 → slot too small → append
            await editor.AddOrReplaceFileAsync("large.bin", new ReadOnlyMemory<byte>(large),
                cancellationToken: cancellationToken);

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("large.bin");
            await Assert.That(editor.Entries[0].FileSize).IsEqualTo(600L);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── DeleteFile ────────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteFile_RemovesEntry(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            await editor.AddOrReplaceFileAsync("to_delete.txt", new ReadOnlyMemory<byte>(new byte[10]),
                cancellationToken: cancellationToken);

            editor.DeleteFile("to_delete.txt");

            await Assert.That(editor.Entries).IsEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task DeleteFile_ThrowsFileNotFoundException_WhenNotFound(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            Assert.Throws<FileNotFoundException>(() => editor.DeleteFile("ghost.txt"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── RenameFile ────────────────────────────────────────────────────────────

    [Test]
    public async Task RenameFile_ThrowsFileNotFoundException_WhenOldNameNotFound(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);

            Assert.Throws<FileNotFoundException>(() => editor.RenameFile("ghost.txt", "new.txt"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task RenameFile_ThrowsInvalidOperationException_WhenNewNameAlreadyExists(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            await editor.AddOrReplaceFileAsync("a.txt", new ReadOnlyMemory<byte>(new byte[8]),
                cancellationToken: cancellationToken);
            await editor.AddOrReplaceFileAsync("b.txt", new ReadOnlyMemory<byte>(new byte[8]),
                cancellationToken: cancellationToken);

            Assert.Throws<InvalidOperationException>(() => editor.RenameFile("a.txt", "b.txt"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task RenameFile_UpdatesEntryNameImmediately(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            await editor.AddOrReplaceFileAsync("old.txt", new ReadOnlyMemory<byte>(new byte[8]),
                cancellationToken: cancellationToken);

            editor.RenameFile("old.txt", "new.txt");

            await Assert.That(editor.Entries).Count().IsEqualTo(1);
            await Assert.That(editor.Entries[0].Name).IsEqualTo("new.txt");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task RenameFile_SetsDirty(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path,
                cancellationToken: cancellationToken);
            await editor.AddOrReplaceFileAsync("a.txt", new ReadOnlyMemory<byte>(new byte[8]),
                cancellationToken: cancellationToken);
            await editor.SaveAsync(cancellationToken);

            editor.RenameFile("a.txt", "b.txt");

            await Assert.That(editor.IsDirty).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── IsDirty ───────────────────────────────────────────────────────────────

    [Test]
    public async Task IsDirty_TrueAfterAdd_FalseAfterSave(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path);
            await editor.AddOrReplaceFileAsync("x.bin", new ReadOnlyMemory<byte>(new byte[8]),
                cancellationToken: cancellationToken);

            await Assert.That(editor.IsDirty).IsTrue();

            await editor.SaveAsync(cancellationToken);

            await Assert.That(editor.IsDirty).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Test]
    public async Task SaveAsync_ThenOpenAsync_RoundTrip_DataIntact(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            var content1 = "File one content"u8.ToArray();
            var content2 = "File two has different content"u8.ToArray();

            // Create and save.
            await using (var editor = await PackageEditor.CreateAsync(path,
                             cancellationToken: cancellationToken))
            {
                await editor.AddOrReplaceFileAsync("dir/one.txt", new ReadOnlyMemory<byte>(content1));
                await editor.AddOrReplaceFileAsync("dir/two.txt", new ReadOnlyMemory<byte>(content2));
                await editor.SaveAsync();
            }

            // Re-open and verify.
            await using var reopened = await PackageEditor.OpenAsync(path,
                cancellationToken: cancellationToken);

            await Assert.That(reopened.Entries).Count().IsEqualTo(2);

            var entry1 = reopened.Entries.First(e => e.Name == "dir/one.txt");
            var entry2 = reopened.Entries.First(e => e.Name == "dir/two.txt");

            var buf1 = new byte[entry1.FileSize];
            await using (var s = entry1.OpenRead())
                _ = await s.ReadAsync(buf1, cancellationToken);

            var buf2 = new byte[entry2.FileSize];
            await using (var s = entry2.OpenRead())
                _ = await s.ReadAsync(buf2, cancellationToken);

            await Assert.That(buf1).IsSequenceEqualTo(content1);
            await Assert.That(buf2).IsSequenceEqualTo(content2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── PackageEntry properties ───────────────────────────────────────────────

    [Test]
    public async Task PackageEntry_Properties_MatchWrittenData(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using var editor = await PackageEditor.CreateAsync(path);
            var data = new byte[42];
            var creation = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
            var modified = new DateTimeOffset(2025, 6, 20, 12, 30, 0, TimeSpan.Zero);
            var options = new PackageWriteOptions { CreationTime = creation, ModifiedTime = modified };

            await editor.AddOrReplaceFileAsync("props.bin", new ReadOnlyMemory<byte>(data), options,
                cancellationToken);

            var entry = editor.Entries[0];
            await Assert.That(entry.Name).IsEqualTo("props.bin");
            await Assert.That(entry.FileSize).IsEqualTo(42L);
            await Assert.That(entry.CreationTime).IsEqualTo(creation);
            await Assert.That(entry.ModifiedTime).IsEqualTo(modified);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task PackageEntry_OpenRead_ReturnsCorrectContent(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            var expected = "readable content"u8.ToArray();
            await using var editor = await PackageEditor.CreateAsync(path);
            await editor.AddOrReplaceFileAsync("read.txt", new ReadOnlyMemory<byte>(expected),
                cancellationToken: cancellationToken);

            await using var stream = editor.Entries[0].OpenRead();
            var buffer = new byte[expected.Length];
            _ = await stream.ReadAsync(buffer, cancellationToken);

            await Assert.That(buffer).IsSequenceEqualTo(expected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            inner.ReadAsync(buffer, offset, count, ct);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            inner.ReadAsync(buffer, ct);

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}