using AAPakFile.Editing;

namespace AAPakFile.Core;

public class FileTableHelperTests
{
    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pak");

    [Test]
    public async Task LoadRecordsAsync_StreamOverload_ReturnsCorrectRecords(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using (var editor = await PackageEditor.CreateAsync(path, cancellationToken: cancellationToken))
            {
                await editor.AddOrReplaceFileAsync("one.txt", new ReadOnlyMemory<byte>("one"u8.ToArray()), cancellationToken: cancellationToken);
                await editor.AddOrReplaceFileAsync("two.txt", new ReadOnlyMemory<byte>("two"u8.ToArray()), cancellationToken: cancellationToken);
                await editor.SaveAsync(cancellationToken);
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 4096, useAsync: true);
            var records = await FileTableHelper.LoadRecordsAsync(stream, cancellationToken: cancellationToken);
            var list = records.ToList();

            await Assert.That(list).Count().IsEqualTo(2);

            var names = list.Select(r => r.FileName.ToString()).ToList();
            await Assert.That(names.Contains("one.txt")).IsTrue();
            await Assert.That(names.Contains("two.txt")).IsTrue();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task LoadRecordsAsync_StreamOverload_EmptyPackage_ReturnsEmpty(CancellationToken cancellationToken)
    {
        var path = NewTempPath();
        try
        {
            await using (var editor = await PackageEditor.CreateAsync(path, cancellationToken: cancellationToken))
            {
                await editor.SaveAsync(cancellationToken);
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 4096, useAsync: true);
            var records = await FileTableHelper.LoadRecordsAsync(stream, cancellationToken: cancellationToken);

            await Assert.That(records.ToList()).IsEmpty();
        }
        finally { File.Delete(path); }
    }
}