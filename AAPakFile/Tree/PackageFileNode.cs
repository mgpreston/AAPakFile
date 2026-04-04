namespace AAPakFile.Tree;

/// <summary>
/// A file leaf node in a PAK directory tree.
/// </summary>
/// <typeparam name="TEntry">The entry type, e.g. <see cref="AAPakFile.Reading.PackageReaderEntry"/>.</typeparam>
public sealed class PackageFileNode<TEntry> : PackageTreeNode<TEntry>
{
    internal PackageFileNode(string name, TEntry entry) : base(name)
    {
        Entry = entry;
    }

    /// <summary>
    /// Gets the underlying PAK entry for this file.
    /// </summary>
    /// <remarks>
    /// The returned object is reference-equal to the entry that was passed to
    /// <see cref="PackageTreeBuilder.Build{TEntry}"/> or that was received via a
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged"/>
    /// add event on the editor's <see cref="AAPakFile.Editing.IPackageEditor.Entries"/>.
    /// </remarks>
    public TEntry Entry { get; }
}