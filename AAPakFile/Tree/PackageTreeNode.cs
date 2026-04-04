namespace AAPakFile.Tree;

/// <summary>
/// An abstract node in a PAK directory tree.
/// </summary>
/// <remarks>
/// Use pattern matching to distinguish the two concrete subtypes:
/// <see cref="PackageDirectoryNode{TEntry}"/> represents a virtual directory, and
/// <see cref="PackageFileNode{TEntry}"/> represents a file.
/// </remarks>
/// <typeparam name="TEntry">
/// The entry type, such as <see cref="AAPakFile.Reading.PackageReaderEntry"/> or
/// <see cref="AAPakFile.Editing.PackageEntry"/>.
/// </typeparam>
public abstract class PackageTreeNode<TEntry>
{
    private protected PackageTreeNode(string name) => Name = name;

    /// <summary>
    /// Gets the unqualified name of this path segment.
    /// For example, <c>"textures"</c> for a directory or <c>"sky.dds"</c> for a file.
    /// The root node's name is <see cref="string.Empty"/>.
    /// </summary>
    public string Name { get; }
}