using System.IO.Compression;

using AAPakFile.Chunking;
using AAPakFile.Compaction;
using AAPakFile.Core;
using AAPakFile.Editing;
using AAPakFile.Export;
using AAPakFile.Import;
using AAPakFile.Integrity;
using AAPakFile.Reading;

namespace AAPakFile;

/// <summary>
/// Provides static methods for importing, exporting, editing, and verifying package files.
/// </summary>
public static class PackageFile
{
    /// <summary>
    /// Imports all files from the specified folder into an existing package, maintaining the source
    /// directory hierarchy. Files already present in the package are replaced; files not present in
    /// the source folder are left untouched.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="sourceFolder">The path to the folder containing files to import.</param>
    /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while accessing the package or source files.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at <paramref name="packagePath"/>
    /// or the folder at <paramref name="sourceFolder"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist, or
    /// <paramref name="sourceFolder"/> does not exist.
    /// </exception>
    public static async Task ImportFromFolderAsync(string packagePath, string sourceFolder,
        ReadOnlyMemory<byte> xlGamesKey = default, IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var importer = new FileImporter();
        await importer.ImportAllFromFolderAsync(packagePath, sourceFolder, xlGamesKey, progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Imports all files from the specified ZIP archive into an existing package, maintaining the
    /// archive's directory hierarchy. Files already present in the package are replaced; files not
    /// present in the archive are left untouched.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="zipFilePath">The path to the ZIP archive containing files to import.</param>
    /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while accessing the package or ZIP archive.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at <paramref name="packagePath"/>
    /// or <paramref name="zipFilePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> or <paramref name="zipFilePath"/> does not exist.
    /// </exception>
    public static async Task ImportFromZipArchiveAsync(string packagePath, string zipFilePath,
        ReadOnlyMemory<byte> xlGamesKey = default, IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var importer = new ZipImporter();
        await importer.ImportAllFromZipArchiveAsync(packagePath, zipFilePath, xlGamesKey, progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new package file from all files in the specified folder, maintaining the source
    /// directory hierarchy. If a file already exists at <paramref name="packagePath"/> it is overwritten.
    /// </summary>
    /// <param name="packagePath">The path of the package file to create.</param>
    /// <param name="sourceFolder">The path to the folder containing files to import.</param>
    /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while creating the package or reading source files.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to create the file at <paramref name="packagePath"/>
    /// or access the folder at <paramref name="sourceFolder"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist, or
    /// <paramref name="sourceFolder"/> does not exist.
    /// </exception>
    public static async Task CreateFromFolderAsync(string packagePath, string sourceFolder,
        ReadOnlyMemory<byte> xlGamesKey = default, IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var importer = new FileImporter();
        await importer.CreateFromFolderAsync(packagePath, sourceFolder, xlGamesKey, progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new package file from all files in the specified ZIP archive, maintaining the
    /// archive's directory hierarchy. If a file already exists at <paramref name="packagePath"/> it is overwritten.
    /// </summary>
    /// <param name="packagePath">The path of the package file to create.</param>
    /// <param name="zipFilePath">The path to the ZIP archive containing files to import.</param>
    /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while creating the package or reading the ZIP archive.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to create the file at <paramref name="packagePath"/>
    /// or access the file at <paramref name="zipFilePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> or <paramref name="zipFilePath"/> does not exist.
    /// </exception>
    public static async Task CreateFromZipArchiveAsync(string packagePath, string zipFilePath,
        ReadOnlyMemory<byte> xlGamesKey = default, IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var importer = new ZipImporter();
        await importer.CreateFromZipArchiveAsync(packagePath, zipFilePath, xlGamesKey, progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports all files from the package to the specified output folder, maintaining the package's directory
    /// hierarchy.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="outputPath">The path to the output directory.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="filter">An optional predicate to select which files to export. If <see langword="null"/>, all files are exported.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while reading the package or writing exported files.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at <paramref name="packagePath"/>
    /// or write to <paramref name="outputPath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static async Task ExportToFolderAsync(string packagePath, string outputPath,
        ReadOnlyMemory<byte> xlGamesKey = default, Func<PackedFileRecord, bool>? filter = null,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var exporter = new FileExporter(new ChunkedFileProcessor());
        await exporter.ExportAllToFolderAsync(packagePath, outputPath, xlGamesKey, filter, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Exports all files from the package to a Zip file, maintaining the package's directory hierarchy.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="zipFilePath">The path to the output Zip file.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="filter">An optional predicate to select which files to export. If <see langword="null"/>, all files are exported.</param>
    /// <param name="compressionLevel">The compression level of the Zip file.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while reading the package or writing the ZIP archive.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at <paramref name="packagePath"/>
    /// or create the file at <paramref name="zipFilePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> or <paramref name="zipFilePath"/> does not exist.
    /// </exception>
    public static async Task ExportToZipArchiveAsync(string packagePath, string zipFilePath,
        ReadOnlyMemory<byte> xlGamesKey = default, Func<PackedFileRecord, bool>? filter = null,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var exporter = new ZipExporter(new ChunkedFileProcessor());
        await exporter.ExportAllToZipArchiveAsync(packagePath, zipFilePath, xlGamesKey, filter, compressionLevel,
            progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the specified package file for reading with shared read access.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="PackageReader"/> with its entries fully populated.
    /// Multiple readers may be open on the same file simultaneously.
    /// Dispose the reader to release the file handle.
    /// </returns>
    /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static Task<PackageReader> OpenReaderAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, CancellationToken cancellationToken = default) =>
        PackageReader.OpenAsync(packagePath, xlGamesKey, cancellationToken);

    /// <summary>
    /// Opens a ZIP archive for reading, exposing its entries via <see cref="Reading.IPackageReader"/>.
    /// </summary>
    /// <param name="zipFilePath">The path to the ZIP archive.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Reading.ZipPackageReader"/> with its entries fully populated.
    /// Directory entries (names ending with '/') are excluded.
    /// Dispose the reader when done; it holds no persistent file handle.
    /// </returns>
    /// <remarks>
    /// <see cref="Reading.PackageReaderEntry.Md5Hash"/> returns an empty span for all entries —
    /// ZIP archives do not store MD5 hashes. <see cref="Reading.PackageReaderEntry.OpenRead"/>
    /// returns a non-seekable stream and opens the ZIP file independently on each call.
    /// </remarks>
    /// <exception cref="IOException">An I/O error occurred while opening the ZIP archive.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access <paramref name="zipFilePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="zipFilePath"/> does not exist.
    /// </exception>
    public static Task<ZipPackageReader> OpenZipReaderAsync(string zipFilePath,
        CancellationToken cancellationToken = default) =>
        ZipPackageReader.OpenAsync(zipFilePath, cancellationToken);

    /// <summary>
    /// Opens an ArcheRage <c>game_pak</c> file for reading, scanning all embedded
    /// CryTek sub-archives and returning a reader with its entries fully populated.
    /// </summary>
    /// <param name="path">The path to the <c>game_pak</c> file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="GamePakReader"/> whose <see cref="Reading.IPackageReader.Entries"/>
    /// contains one <see cref="Reading.PackageReaderEntry"/> per data block across all
    /// sub-archives. Entry names follow the pattern
    /// <c>{archiveIndex:D6}_{entryIndex:D4}_attr{attr:D2}</c>.
    /// Dispose the reader to release the underlying file handle.
    /// </returns>
    /// <exception cref="FormatException">
    /// The file does not begin with a valid CryTek archive header.
    /// </exception>
    /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="path"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="path"/> does not exist.
    /// </exception>
    internal static Task<GamePakReader> OpenGamePakAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        GamePakReader.OpenAsync(path, cancellationToken);

    /// <summary>
    /// Opens a single file inside the specified package for reading.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="fileName">
    /// The name of the file to open, including any path information (e.g. <c>"game/textures/sky.dds"</c>).
    /// </param>
    /// <param name="xlGamesKey">The AES decryption key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Stream"/> positioned at the start of the file's data. The caller is responsible for
    /// disposing the stream; disposing it also releases the underlying file handle.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    /// No file named <paramref name="fileName"/> exists in the package.
    /// </exception>
    /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static async Task<Stream> OpenFileAsync(string packagePath, string fileName,
        ReadOnlyMemory<byte> xlGamesKey = default, CancellationToken cancellationToken = default)
    {
        var handle = File.OpenHandle(packagePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, FileOptions.Asynchronous);
        try
        {
            var records = await FileTableHelper
                .LoadRecordsAsync(handle, xlGamesKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (var record in records)
            {
                if (record.FileName.ToString() == fileName)
                    return new PackedFileStream(handle, record.FileOffset, record.FileSize, ownsHandle: true);
            }

            throw new FileNotFoundException($"'{fileName}' was not found in the package.", fileName);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Verifies the integrity of all files in the specified package by checking each file's MD5 hash.
    /// All files are verified, even if damaged files are encountered.
    /// </summary>
    /// <param name="packagePath">The path to the package file to verify.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous iteration over the results of the integrity check.</returns>
    /// <exception cref="IOException">An I/O error occurred while reading the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static IAsyncEnumerable<BulkPackedFileIntegrityResult> VerifyAllFilesAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, CancellationToken cancellationToken = default)
    {
        var integrityVerifier = new BulkPackageIntegrityVerifier(new ChunkedFileProcessor());
        return integrityVerifier.VerifyAllFilesAsync(packagePath, xlGamesKey, cancellationToken);
    }

    /// <summary>
    /// Creates a new, empty package file and opens it for editing with exclusive read/write access.
    /// If a file already exists at <paramref name="packagePath"/> it is overwritten.
    /// </summary>
    /// <param name="packagePath">The path of the package file to create.</param>
    /// <param name="xlGamesKey">The AES encryption key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="fileCopyBufferSize">The buffer size used for file copy operations. Defaults to 80 KiB.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="PackageEditor"/> that can be used to add files to the new package.
    /// Dispose the editor to release the file handle. Call <see cref="PackageEditor.SaveAsync"/> to
    /// write the file table before disposing.
    /// </returns>
    /// <exception cref="IOException">An I/O error occurred while creating the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to create or overwrite the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static Task<PackageEditor> CreateEditorAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, int fileCopyBufferSize = 80 * 1024,
        CancellationToken cancellationToken = default) =>
        PackageEditor.CreateAsync(packagePath, xlGamesKey, fileCopyBufferSize, cancellationToken);

    /// <summary>
    /// Opens the specified package file for editing with exclusive read/write access.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="fileCopyBufferSize">The buffer size used for file copy operations. Defaults to 80 KiB.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="PackageEditor"/> that can be used to add, replace, and delete files in the package.
    /// Dispose the editor to release the file handle. Call <see cref="PackageEditor.SaveAsync"/> to
    /// persist changes before disposing.
    /// </returns>
    /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static Task<PackageEditor> OpenEditorAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, int fileCopyBufferSize = 80 * 1024,
        CancellationToken cancellationToken = default) =>
        PackageEditor.OpenAsync(packagePath, xlGamesKey, fileCopyBufferSize, cancellationToken);

    /// <summary>
    /// Verifies the integrity of all files in the specified package by checking each file's MD5 hash.
    /// Stops on the first invalid file.
    /// </summary>
    /// <param name="packagePath">The path to the package file to verify.</param>
    /// <param name="xlGamesKey">The AES decryption key for the package.</param>
    /// <param name="progress">Receives progress updates as files are verified.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns> A <see cref="PackageIntegrityResult"/> containing the result of the verification.</returns>
    /// <exception cref="IOException">An I/O error occurred while reading the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static async Task<PackageIntegrityResult> VerifyPackageAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, IProgress<VerifyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var integrityVerifier = new BulkPackageIntegrityVerifier(new ChunkedFileProcessor());
        return await integrityVerifier
            .VerifyPackageAsync(packagePath, xlGamesKey, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rewrites the package to a temporary file in the same directory, removing all unused gaps,
    /// then atomically replaces the original.
    /// </summary>
    /// <remarks>
    /// Requires approximately the same amount of free disk space as the original package.
    /// The original file is never modified if the operation fails or is cancelled.
    /// </remarks>
    /// <param name="packagePath">The path to the package file to compact.</param>
    /// <param name="xlGamesKey">The AES key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while compacting the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static async Task CompactAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, IProgress<CompactProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var compactor = new PackageCompactor();
        await compactor.CompactAsync(packagePath, xlGamesKey, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes unused gaps by shifting file data in-place within the same file, then rewrites
    /// the file table and truncates.
    /// </summary>
    /// <remarks>
    /// Does not require extra disk space. If interrupted after data has been moved but before the
    /// file table is updated, the file will be corrupt and unrecoverable. Use <see cref="CompactAsync"/>
    /// when data integrity on failure is required.
    /// </remarks>
    /// <param name="packagePath">The path to the package file to compact.</param>
    /// <param name="xlGamesKey">The AES key for the package. If empty, the default XLGames key is used.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <param name="bufferSize">The buffer size used for data-shift and file-table write operations. Defaults to 80 KiB.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="IOException">An I/O error occurred while compacting the file.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The caller does not have the required permission to access the file at
    /// <paramref name="packagePath"/>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory portion of <paramref name="packagePath"/> does not exist.
    /// </exception>
    public static async Task CompactInPlaceAsync(string packagePath,
        ReadOnlyMemory<byte> xlGamesKey = default, IProgress<CompactProgress>? progress = null,
        int bufferSize = 80 * 1024, CancellationToken cancellationToken = default)
    {
        var compactor = new PackageCompactor();
        await compactor.CompactInPlaceAsync(packagePath, xlGamesKey, progress, bufferSize,
            cancellationToken).ConfigureAwait(false);
    }
}