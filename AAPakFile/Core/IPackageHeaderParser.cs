using System.Diagnostics.CodeAnalysis;

namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for parsing the header of a package from a right-sized buffer.
/// </summary>
public interface IPackageHeaderParser
{
    /// <summary>
    /// Tries to parse a span of bytes into a package header.
    /// </summary>
    /// <param name="data">The span of bytes to parse.</param>
    /// <param name="header">
    /// When this method returns, contains the header parsed from <paramref name="data"/> if the conversion succeeded,
    /// or null if the conversion failed.
    /// This parameter is passed uninitialized; any value originally supplied in result will be overwritten.
    /// </param>
    /// <returns>true if the header was parsed successfully; otherwise, false.</returns>
    bool TryParse(Span<byte> data, [NotNullWhen(true)] out PackageHeader? header);

    /// <summary>
    /// Parses a span of bytes into a package header.
    /// </summary>
    /// <param name="data">The span of bytes to parse.</param>
    /// <returns>The header parsed from <paramref name="data"/>.</returns>
    /// <exception cref="ArgumentException">The length of <paramref name="data"/> is not right-sized.</exception>
    /// <exception cref="FormatException">The span of bytes does not constitute a valid header.</exception>
    PackageHeader Parse(Span<byte> data);
}