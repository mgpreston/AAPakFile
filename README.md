# AAPakFile

[![NuGet](https://img.shields.io/nuget/v/AAPakFile.svg)](https://www.nuget.org/packages/AAPakFile)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![AOT Compatible](https://img.shields.io/badge/AOT-compatible-512BD4)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
[![CI](https://github.com/mgpreston/AAPakFile/actions/workflows/ci.yml/badge.svg)](https://github.com/mgpreston/AAPakFile/actions/workflows/ci.yml)
[![codecov](https://codecov.io/github/mgpreston/AAPakFile/graph/badge.svg?token=Q0M7ZLXQE4)](https://codecov.io/github/mgpreston/AAPakFile)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Renovate](https://img.shields.io/badge/renovate-enabled-brightgreen.svg)](https://renovateapp.com/)

A .NET library for reading, creating, and manipulating `game_pak` files from the MMORPG ArcheAge.

## Installation

```
dotnet add package AAPakFile
```

Requires .NET 10.0 or later. AOT-compatible.

## Usage

All operations are exposed through the static `PackageFile` class. Every method is async and accepts an optional `CancellationToken` and `IProgress<T>` for progress reporting.

### Export a package to a folder

```csharp
await PackageFile.ExportToFolderAsync("game_pak", outputPath: "extracted/");
```

### Export a package to a ZIP archive

```csharp
await PackageFile.ExportToZipArchiveAsync("game_pak", "extracted.zip");
```

### Import files from a folder into an existing package

Files present in the package but absent from the source folder are left untouched.

```csharp
await PackageFile.ImportFromFolderAsync("game_pak", sourceFolder: "mods/");
```

### Import files from a ZIP archive into an existing package

```csharp
await PackageFile.ImportFromZipArchiveAsync("game_pak", "mods.zip");
```

### Create a new package from a folder

```csharp
await PackageFile.CreateFromFolderAsync("output.pak", sourceFolder: "files/");
```

### Open a package for reading

```csharp
await using var reader = await PackageFile.OpenReaderAsync("game_pak");
foreach (var entry in reader.Entries)
{
    Console.WriteLine(entry.FileName);
}
```

### Open a single file inside a package

```csharp
await using var stream = await PackageFile.OpenFileAsync("game_pak", "game/textures/sky.dds");
```

### Edit a package

```csharp
await using var editor = await PackageFile.OpenEditorAsync("game_pak");
await editor.AddOrReplaceAsync("game/textures/sky.dds", File.OpenRead("sky.dds"));
await editor.SaveAsync();
```

### Verify package integrity

```csharp
// Quick — stops on the first invalid file
var result = await PackageFile.VerifyPackageAsync("game_pak");
Console.WriteLine(result.IsValid ? "OK" : $"Corrupt: {result.InvalidFile}");

// Full — reports every file
await foreach (var fileResult in PackageFile.VerifyAllFilesAsync("game_pak"))
{
    if (!fileResult.IsValid)
        Console.WriteLine($"Corrupt: {fileResult.FileName}");
}
```

### Compact a package

Deleted and replaced files leave gaps; compaction reclaims that space.

```csharp
// Safe: writes to a temp file, then atomically replaces the original.
// Requires ~same free disk space as the package.
await PackageFile.CompactAsync("game_pak");

// Fast: shifts data in-place. No extra disk space required,
// but the file is unrecoverable if interrupted mid-operation.
await PackageFile.CompactInPlaceAsync("game_pak");
```

## License

MIT — see [LICENSE](LICENSE).
