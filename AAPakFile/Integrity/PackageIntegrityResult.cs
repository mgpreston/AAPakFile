namespace AAPakFile.Integrity;

/// <summary>
/// The result of verifying the integrity of a package.
/// </summary>
/// <param name="Success"><c>true</c> if all checked files passed MD5 verification; otherwise <c>false</c>.</param>
/// <param name="InvalidRecord">
/// The first file record that failed verification, or <c>null</c> if none failed verification.
/// </param>
public record PackageIntegrityResult(bool Success, PackedFileRecord? InvalidRecord);