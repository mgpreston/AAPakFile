using System.Collections.ObjectModel;

namespace AAPakFile.Tree;

/// <summary>
/// A virtual directory node in a PAK directory tree.
/// </summary>
/// <remarks>
/// Directory nodes are virtual — they have no corresponding entry in the PAK file.
/// Their <see cref="Children"/> are kept sorted: all
/// <see cref="PackageDirectoryNode{TEntry}"/> instances appear before all
/// <see cref="PackageFileNode{TEntry}"/> instances, with each group sorted
/// case-insensitively by <see cref="PackageTreeNode{TEntry}.Name"/>.
/// </remarks>
/// <typeparam name="TEntry">The entry type held by file leaf nodes.</typeparam>
public sealed class PackageDirectoryNode<TEntry> : PackageTreeNode<TEntry>
{
    internal PackageDirectoryNode(string name) : base(name)
    {
        MutableChildren = [];
        Children = new ReadOnlyObservableCollection<PackageTreeNode<TEntry>>(MutableChildren);
    }

    /// <summary>
    /// Gets the children of this directory node.
    /// </summary>
    /// <remarks>
    /// Fires <see cref="System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged"/>
    /// with targeted add / remove events when <see cref="PackageTreeView{TEntry}"/> updates the tree.
    /// Suitable for direct data-binding in WPF, Avalonia, MAUI, and similar frameworks.
    /// </remarks>
    public ReadOnlyObservableCollection<PackageTreeNode<TEntry>> Children { get; }

    /// <summary>
    /// Gets the mutable backing collection. For internal use by
    /// <see cref="PackageTreeBuilder"/> and <see cref="PackageTreeView{TEntry}"/> only.
    /// </summary>
    internal ObservableCollection<PackageTreeNode<TEntry>> MutableChildren { get; }
}