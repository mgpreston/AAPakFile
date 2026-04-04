using Microsoft.Win32.SafeHandles;

namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for a reader that can parse a package header from a file handle.
/// </summary>
public interface IFilePackageHeaderReader
{
    /// <summary>
    /// Reads the header from a package via its file handle.
    /// </summary>
    /// <param name="packageHandle">The handle to the package file.</param>
    /// <returns>The package header that was read.</returns>
    /// <exception cref="FormatException">The package header is invalid.</exception>
    /// <exception cref="InvalidDataException">The package file does not contain enough data.</exception>
    /// <exception cref="IOException">An I/O error occurred.</exception>
    /// <exception cref="NotSupportedException">The file does not support seeking (pipe or socket).</exception>
    /// <exception cref="UnauthorizedAccessException"><paramref name="packageHandle"/> was not opened for reading.</exception>
    PackageHeader ReadHeader(SafeFileHandle packageHandle);
}