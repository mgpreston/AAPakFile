using AAPakFile.Editing;

namespace AAPakFile.Integration;

/// <summary>
/// Defines the known content for each integration-test fixture package and provides
/// factory methods to create those packages on disk.
/// </summary>
internal static class IntegrationFixtures
{
    // ── Fixed timestamp ───────────────────────────────────────────────────────────
    // Using a fixed point in time makes the serialised timestamps deterministic so
    // tests can assert exact values rather than "roughly now".
    public static readonly DateTimeOffset FixedTime =
        new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly PackageWriteOptions FixedOptions = new()
    {
        CreationTime = FixedTime,
        ModifiedTime = FixedTime,
    };

    // ── Known file contents ───────────────────────────────────────────────────────
    public static readonly byte[] HelloContent = "Hello, World!"u8.ToArray();
    public static readonly byte[] ReadmeContent = "This is a readme."u8.ToArray();
    public static readonly byte[] ValuesContent = [1, 2, 3, 4, 5];
    public static readonly byte[] ConfigContent = "key=value"u8.ToArray();

    // ── Package builders ──────────────────────────────────────────────────────────

    /// <summary>Creates a package that contains no files.</summary>
    public static async Task CreateEmptyAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var editor = await PackageEditor.CreateAsync(path, cancellationToken: cancellationToken);
        await editor.SaveAsync(cancellationToken);
    }

    /// <summary>Creates a package containing a single file: <c>hello.txt</c>.</summary>
    public static async Task CreateSingleFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var editor = await PackageEditor.CreateAsync(path, cancellationToken: cancellationToken);
        await editor.AddOrReplaceFileAsync("hello.txt",
            new ReadOnlyMemory<byte>(HelloContent), FixedOptions, cancellationToken);
        await editor.SaveAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a package with three files across two directories:
    /// <c>readme.txt</c>, <c>data/values.bin</c>, and <c>data/config.txt</c>.
    /// </summary>
    public static async Task CreateMultiFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var editor = await PackageEditor.CreateAsync(path, cancellationToken: cancellationToken);
        await editor.AddOrReplaceFileAsync("readme.txt",
            new ReadOnlyMemory<byte>(ReadmeContent), FixedOptions, cancellationToken);
        await editor.AddOrReplaceFileAsync("data/values.bin",
            new ReadOnlyMemory<byte>(ValuesContent), FixedOptions, cancellationToken);
        await editor.AddOrReplaceFileAsync("data/config.txt",
            new ReadOnlyMemory<byte>(ConfigContent), FixedOptions, cancellationToken);
        await editor.SaveAsync(cancellationToken);
    }
}