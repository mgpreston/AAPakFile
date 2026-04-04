using AAPakFile.Editing;
using AAPakFile.Reading;

namespace AAPakFile.Tree;

/// <summary>
/// Extension methods that add tree-building capabilities to
/// <see cref="IPackageReader"/> and <see cref="IPackageEditor"/>.
/// </summary>
public static class PackageTreeExtensions
{
    /// <summary>
    /// Builds a snapshot directory tree from all entries in this reader.
    /// </summary>
    /// <param name="reader">The reader whose entries to organise.</param>
    /// <returns>
    /// The root <see cref="PackageDirectoryNode{TEntry}"/> of the tree.
    /// This is a static snapshot; it does not update if the underlying file changes.
    /// </returns>
    public static PackageDirectoryNode<PackageReaderEntry> BuildTree(this IPackageReader reader)
        => PackageTreeBuilder.Build(reader.Entries);

    /// <summary>
    /// Creates a live <see cref="PackageTreeView{TEntry}"/> that tracks mutations
    /// to the editor's <see cref="IPackageEditor.Entries"/> collection.
    /// </summary>
    /// <param name="editor">The editor whose entries to track.</param>
    /// <returns>
    /// A <see cref="PackageTreeView{TEntry}"/> whose <see cref="PackageTreeView{TEntry}.Root"/>
    /// is data-binding-ready and updates automatically as entries are added, renamed, or deleted.
    /// Dispose the view when it is no longer needed.
    /// </returns>
    public static PackageTreeView<PackageEntry> BuildTree(this IPackageEditor editor)
        => new(editor.Entries, static e => e.Name);
}