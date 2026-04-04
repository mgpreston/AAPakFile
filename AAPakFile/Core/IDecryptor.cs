using System.Security.Cryptography;

namespace AAPakFile.Core;

/// <summary>
/// Defines an interface for the decryption of encrypted sections of packages.
/// </summary>
public interface IDecryptor
{
    /// <summary>
    /// Decrypts the given ciphertext representing an encrypted section of a package, and stores the result in
    /// <paramref name="destination"/>.
    /// </summary>
    /// <param name="ciphertext">The span of bytes containing the encrypted data.</param>
    /// <param name="destination">
    /// A span at least as large as <paramref name="ciphertext"/> to store the decrypted data in.
    /// </param>
    /// <exception cref="CryptographicException">The ciphertext could not be decrypted successfully.</exception>
    /// <exception cref="ArgumentException">
    /// The buffer in <paramref name="destination"/> is too small to hold the plaintext data.
    /// </exception>
    void Decrypt(ReadOnlySpan<byte> ciphertext, Span<byte> destination);
}