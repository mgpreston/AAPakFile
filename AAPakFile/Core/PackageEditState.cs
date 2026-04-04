namespace AAPakFile.Core;

/// <summary>
/// Holds all state required to edit an open package.
/// </summary>
internal record PackageEditState(
    PackageHeader Header,
    List<PackedFileRecord> Files,
    List<PackedFileRecord> ExtraFiles,
    long FirstFileInfoOffset);