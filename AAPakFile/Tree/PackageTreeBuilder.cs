using AAPakFile.Editing;
using AAPakFile.Reading;

namespace AAPakFile.Tree;

/// <summary>
/// Builds an in-memory directory tree from a flat sequence of PAK entries.
/// </summary>
/// <remarks>
/// The resulting tree is a snapshot: it does not update when entries are subsequently
/// added, renamed, or deleted. For a live, data-bindable view that updates in response
/// to editor mutations, use <see cref="PackageTreeView{TEntry}"/> via
/// <see cref="PackageTreeExtensions.BuildTree(AAPakFile.Editing.IPackageEditor)"/>.
/// </remarks>
public static class PackageTreeBuilder
{
    /// <summary>
    /// Builds a directory tree from a sequence of entries using a custom path selector.
    /// </summary>
    /// <typeparam name="TEntry">The entry type.</typeparam>
    /// <param name="entries">The flat entry sequence to organise into a tree.</param>
    /// <param name="nameSelector">
    /// A delegate that returns the forward-slash-delimited path for an entry
    /// (e.g. <c>e =&gt; e.Name</c>). Backslashes are treated as literal characters,
    /// not separators.
    /// </param>
    /// <returns>
    /// The root <see cref="PackageDirectoryNode{TEntry}"/> whose
    /// <see cref="PackageTreeNode{TEntry}.Name"/> is <see cref="string.Empty"/>.
    /// Children are sorted: directories first (case-insensitive ordinal), then files
    /// (case-insensitive ordinal).
    /// </returns>
    public static PackageDirectoryNode<TEntry> Build<TEntry>(
        IEnumerable<TEntry> entries,
        Func<TEntry, string> nameSelector)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(nameSelector);

        var root = new MutableDir<TEntry>(string.Empty);

        foreach (var entry in entries)
        {
            var name = nameSelector(entry);
            // nameSelector is declared to return non-null string, so NRT-aware callers will never
            // reach this. ThrowIfNull (which accepts object?) guards against callers compiled
            // without nullable reference type annotations who could return null at runtime,
            // giving a clear ArgumentNullException instead of a NullReferenceException later.
            ArgumentNullException.ThrowIfNull(name, nameof(nameSelector));
            InsertEntry(root, name, entry);
        }

        return root.ToNode();
    }

    /// <summary>
    /// Builds a directory tree from a sequence of <see cref="PackageReaderEntry"/> instances.
    /// </summary>
    public static PackageDirectoryNode<PackageReaderEntry> Build(
        IEnumerable<PackageReaderEntry> entries)
        => Build(entries, static e => e.Name);

    /// <summary>
    /// Builds a directory tree from a sequence of <see cref="PackageEntry"/> instances.
    /// </summary>
    public static PackageDirectoryNode<PackageEntry> Build(
        IEnumerable<PackageEntry> entries)
        => Build(entries, static e => e.Name);

    // ── Implementation ────────────────────────────────────────────────────────

    private static void InsertEntry<TEntry>(MutableDir<TEntry> root, string fullName, TEntry entry)
    {
        var remaining = fullName.AsSpan();
        var current = root;

        while (true)
        {
            var slashIndex = remaining.IndexOf('/');

            if (slashIndex < 0)
            {
                // Reached the final file-name segment (no more slashes).
                if (remaining.IsEmpty) break; // entry whose name ends with '/' — skip

                // Reuse the existing heap string rather than allocating a new one:
                // if `remaining` covers the whole `fullName`, that IS fullName;
                // otherwise take a substring from fullName starting at the correct offset.
                var segmentName = remaining.Length == fullName.Length
                    ? fullName
                    : fullName[^remaining.Length..];

                current.UpsertFile(segmentName, entry);
                break;
            }

            var segment = remaining[..slashIndex];
            remaining = remaining[(slashIndex + 1)..];

            if (segment.IsEmpty) continue; // skip leading or consecutive slashes

            current = current.GetOrCreateSubDir(segment);
        }
    }

    // ── Mutable working types ─────────────────────────────────────────────────

    /// <summary>Mutable directory built up during a single <see cref="Build{TEntry}"/> call.</summary>
    private sealed class MutableDir<TEntry>
    {
        // Ordinal (case-sensitive) key lookup preserves the original casing of directory names.
        // GetAlternateLookup<ReadOnlySpan<char>>() avoids allocating a string for each lookup.
        private readonly Dictionary<string, MutableDir<TEntry>> _subDirs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string Name, TEntry Entry)> _files = new(StringComparer.Ordinal);

        internal MutableDir(string name) => Name = name;

        internal string Name { get; }

        /// <summary>
        /// Returns the sub-directory for <paramref name="segment"/>, creating it if absent.
        /// No string is allocated when the directory already exists.
        /// </summary>
        internal MutableDir<TEntry> GetOrCreateSubDir(ReadOnlySpan<char> segment)
        {
            // GetAlternateLookup is a lightweight struct wrapper — no allocation.
            var lookup = _subDirs.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(segment, out var existing))
                return existing;

            var name = segment.ToString(); // allocate once for new directories only
            var dir = new MutableDir<TEntry>(name);
            _subDirs[name] = dir;
            return dir;
        }

        /// <summary>Adds or replaces the file entry for <paramref name="name"/>.</summary>
        internal void UpsertFile(string name, TEntry entry) => _files[name] = (name, entry);

        /// <summary>
        /// Converts this mutable directory into the immutable, observable
        /// <see cref="PackageDirectoryNode{TEntry}"/> tree.
        /// Children are sorted: directories first (OrdinalIgnoreCase), then files (OrdinalIgnoreCase).
        /// </summary>
        internal PackageDirectoryNode<TEntry> ToNode()
        {
            var node = new PackageDirectoryNode<TEntry>(Name);
            var children = node.MutableChildren;

            foreach (var dir in _subDirs.Values.OrderBy(static d => d.Name, StringComparer.OrdinalIgnoreCase))
                children.Add(dir.ToNode());

            foreach ((string name, TEntry entry) in _files.Values.OrderBy(static f => f.Name, StringComparer.OrdinalIgnoreCase))
                children.Add(new PackageFileNode<TEntry>(name, entry));

            return node;
        }
    }
}