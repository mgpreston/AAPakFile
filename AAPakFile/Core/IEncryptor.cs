using System.Security.Cryptography;

namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for the encryption of sections of packages.
/// </summary>
public interface IEncryptor
{
    /// <summary>
    /// Encrypts the given plaintext representing a section of a package, and stores the result in
    /// <paramref name="destination"/>.
    /// </summary>
    /// <param name="plaintext">The span of bytes containing the plaintext data to encrypt.</param>
    /// <param name="destination">
    /// A span at least as large as <paramref name="plaintext"/> to store the encrypted result in.
    /// </param>
    /// <exception cref="CryptographicException">The plaintext could not be encrypted successfully.</exception>
    /// <exception cref="ArgumentException">
    /// The buffer in <paramref name="destination"/> is too small to hold the ciphertext data.
    /// </exception>
    void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> destination);
}