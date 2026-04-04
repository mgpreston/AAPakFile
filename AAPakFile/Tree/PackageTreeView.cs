using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace AAPakFile.Tree;

/// <summary>
/// A stateful, event-driven view of a PAK directory tree intended for UI data-binding.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Root"/> is a stable object for the lifetime of the view — bind your
/// <c>TreeView</c>'s items source to <see cref="PackageDirectoryNode{TEntry}.Children"/>
/// of <see cref="Root"/> directly. The tree updates itself by subscribing to the
/// <see cref="INotifyCollectionChanged.CollectionChanged"/> event of the supplied entries
/// collection and applying surgical add / remove operations on the affected
/// <see cref="PackageDirectoryNode{TEntry}.Children"/> collections. No <c>Reset</c> event
/// is ever fired on any node's <c>Children</c> collection, so UI state such as expanded /
/// collapsed nodes is preserved at all times.
/// </para>
/// <para>
/// Consumers who wrap nodes in their own ViewModel classes can maintain a
/// <c>Dictionary&lt;PackageDirectoryNode&lt;TEntry&gt;, TheirViewModel&gt;</c>: directory
/// nodes that survive a change are always the same object instance.
/// </para>
/// <para>
/// Dispose this view when it is no longer needed to unsubscribe from the entries collection.
/// </para>
/// </remarks>
/// <typeparam name="TEntry">The entry type held by file leaf nodes.</typeparam>
public sealed class PackageTreeView<TEntry> : IDisposable
{
    private readonly ReadOnlyObservableCollection<TEntry> _source;
    private readonly Func<TEntry, string> _nameSelector;

    // Full directory-path → directory node (root is keyed by "")
    private readonly Dictionary<string, PackageDirectoryNode<TEntry>> _dirIndex =
        new(StringComparer.Ordinal);

    // Full file-path → file node
    private readonly Dictionary<string, PackageFileNode<TEntry>> _fileIndex =
        new(StringComparer.Ordinal);

    private bool _disposed;

    /// <summary>
    /// Initialises a new <see cref="PackageTreeView{TEntry}"/> and builds the initial tree
    /// from the current contents of <paramref name="entries"/>.
    /// </summary>
    /// <param name="entries">
    /// An observable collection of entries to track. The view subscribes to
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> and updates the tree
    /// accordingly.
    /// </param>
    /// <param name="nameSelector">
    /// A delegate that returns the forward-slash-delimited path for an entry.
    /// </param>
    public PackageTreeView(ReadOnlyObservableCollection<TEntry> entries, Func<TEntry, string> nameSelector)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(nameSelector);

        _source = entries;
        _nameSelector = nameSelector;

        Root = new PackageDirectoryNode<TEntry>(string.Empty);
        _dirIndex[string.Empty] = Root;

        // Populate from the current entries. Because no subscriber has bound to Root.Children
        // yet (the binding is established after the constructor returns), these ObservableCollection
        // mutations have no listeners — effectively free for large entry sets.
        foreach (var entry in entries)
            AddEntry(entry);

        ((INotifyCollectionChanged)entries).CollectionChanged += OnSourceChanged;
    }

    /// <summary>
    /// Gets the root directory node. This is the same object for the entire lifetime of
    /// the view.
    /// </summary>
    public PackageDirectoryNode<TEntry> Root { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ((INotifyCollectionChanged)_source).CollectionChanged -= OnSourceChanged;
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (TEntry entry in e.NewItems!)
                    AddEntry(entry);
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (TEntry entry in e.OldItems!)
                    RemoveEntry(_nameSelector(entry));
                break;

            case NotifyCollectionChangedAction.Replace:
                // Remove old, then add new — handles both content-replace and rename.
                foreach (TEntry entry in e.OldItems!)
                    RemoveEntry(_nameSelector(entry));
                foreach (TEntry entry in e.NewItems!)
                    AddEntry(entry);
                break;

            case NotifyCollectionChangedAction.Reset:
                // Defensive path — not fired by DeferrableObservableCollection, but handled
                // for any other ReadOnlyObservableCollection source.
                RebuildFromSource();
                break;
        }
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    private void AddEntry(TEntry entry)
    {
        var fullName = _nameSelector(entry);

        var lastSlash = fullName.LastIndexOf('/');
        var fileName = lastSlash < 0 ? fullName : fullName[(lastSlash + 1)..];
        if (fileName.Length == 0) return; // trailing slash only — skip

        var parentDir = GetOrCreateAncestorDirs(fullName, lastSlash);
        var fileNode = new PackageFileNode<TEntry>(fileName, entry);

        _fileIndex[fullName] = fileNode;
        InsertSorted(parentDir.MutableChildren, fileNode);
    }

    private void RemoveEntry(string fullName)
    {
        if (!_fileIndex.Remove(fullName, out var fileNode)) return;

        var lastSlash = fullName.LastIndexOf('/');
        var parentPath = lastSlash < 0 ? string.Empty : fullName[..lastSlash];

        if (_dirIndex.TryGetValue(parentPath, out var parentDir))
            parentDir.MutableChildren.Remove(fileNode);

        PruneEmptyAncestors(parentPath);
    }

    private void RebuildFromSource()
    {
        // Clear state and rebuild from the current snapshot.
        // This path is only reached for non-DeferrableObservableCollection sources
        // that fire Reset; in normal editor use it is never called.
        _fileIndex.Clear();

        // Clear all directory children (fires Reset on each ObservableCollection — acceptable
        // since this is already a Reset-triggered rebuild).
        foreach (var dir in _dirIndex.Values)
            dir.MutableChildren.Clear();

        _dirIndex.Clear();
        _dirIndex[string.Empty] = Root;

        foreach (var entry in _source)
            AddEntry(entry);
    }

    // ── Directory helpers ─────────────────────────────────────────────────────

    private PackageDirectoryNode<TEntry> GetOrCreateAncestorDirs(string fullName, int lastSlashIndex)
    {
        if (lastSlashIndex < 0) return Root;
        var dirPath = fullName[..lastSlashIndex];
        return GetOrCreateDir(dirPath);
    }

    private PackageDirectoryNode<TEntry> GetOrCreateDir(string dirPath)
    {
        if (_dirIndex.TryGetValue(dirPath, out var existing)) return existing;

        var lastSlash = dirPath.LastIndexOf('/');
        var parentPath = lastSlash < 0 ? string.Empty : dirPath[..lastSlash];
        var dirName = lastSlash < 0 ? dirPath : dirPath[(lastSlash + 1)..];

        var parentDir = GetOrCreateDir(parentPath);
        var newDir = new PackageDirectoryNode<TEntry>(dirName);
        _dirIndex[dirPath] = newDir;
        InsertSorted(parentDir.MutableChildren, newDir);
        return newDir;
    }

    private void PruneEmptyAncestors(string dirPath)
    {
        // Walk upward, removing each now-empty ancestor until we reach the root
        // or an ancestor that still has children. Written as a loop because C# does
        // not guarantee tail-call optimisation, so a recursive version allocates a
        // real stack frame per level.
        while (dirPath.Length > 0)
        {
            if (!_dirIndex.TryGetValue(dirPath, out var dir)) return;
            if (dir.Children.Count > 0) return;

            var lastSlash = dirPath.LastIndexOf('/');
            var parentPath = lastSlash < 0 ? string.Empty : dirPath[..lastSlash];

            if (_dirIndex.TryGetValue(parentPath, out var parentDir))
                parentDir.MutableChildren.Remove(dir);

            _dirIndex.Remove(dirPath);
            dirPath = parentPath;
        }
    }

    // ── Sorted insertion ──────────────────────────────────────────────────────

    /// <summary>
    /// Inserts <paramref name="child"/> into <paramref name="children"/> maintaining
    /// sort order: directories first (OrdinalIgnoreCase), then files (OrdinalIgnoreCase).
    /// </summary>
    private static void InsertSorted(
        ObservableCollection<PackageTreeNode<TEntry>> children,
        PackageTreeNode<TEntry> child)
    {
        var isDir = child is PackageDirectoryNode<TEntry>;
        var insertAt = children.Count; // default: append

        for (var i = 0; i < children.Count; i++)
        {
            var existing = children[i];
            var existingIsDir = existing is PackageDirectoryNode<TEntry>;

            if (isDir && !existingIsDir)
            {
                // New directory must precede all files.
                insertAt = i;
                break;
            }

            if (isDir == existingIsDir &&
                StringComparer.OrdinalIgnoreCase.Compare(child.Name, existing.Name) <= 0)
            {
                insertAt = i;
                break;
            }
        }

        children.Insert(insertAt, child);
    }
}