using System.Security.Cryptography;

namespace AAPakFile.Core;

/// <summary>
/// Provides an implementation for the decryption of package headers or file tables.
/// </summary>
internal sealed class Decryptor : IDisposable, IDecryptor
{
    private readonly Aes _aes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Decryptor"/> class with the given decryption key.
    /// </summary>
    /// <param name="key">The AES decryption key. If empty, <see cref="XlGamesAesKey.Default"/> is used.</param>
    /// <exception cref="CryptographicException">
    ///   <para>
    ///     The key size is invalid.
    ///   </para>
    ///   <para>-or-</para>
    ///   <para>
    ///     An error occurred while setting the key.
    ///   </para>
    /// </exception>
    public Decryptor(ReadOnlySpan<byte> key)
    {
        _aes = Aes.Create();

        if (key.IsEmpty)
        {
            key = XlGamesAesKey.Default.Span;
        }

        _aes.SetKey(key);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Decryptor"/> class with the given AES implementation.
    /// </summary>
    /// <param name="aes">The AES implementation to use for decryption.</param>
    public Decryptor(Aes aes) => _aes = aes;

    /// <summary>
    /// Decrypts the given ciphertext representing a package header or file table entry, and stores the result in
    /// <paramref name="destination"/>.
    /// </summary>
    /// <param name="aes">An AES class that provides the decryption implementation, with the key already set.</param>
    /// <param name="ciphertext">The span of bytes containing the encrypted ciphertext.</param>
    /// <param name="destination">
    /// A span at least as large as <paramref name="ciphertext"/> to store the decrypted result in.
    /// </param>
    /// <exception cref="CryptographicException">The ciphertext could not be decrypted successfully.</exception>
    /// <exception cref="ArgumentException">
    /// The buffer in <paramref name="destination"/> is too small to hold the plaintext data.
    /// </exception>
    public static void Decrypt(Aes aes, ReadOnlySpan<byte> ciphertext, Span<byte> destination)
    {
        Span<byte> iv = stackalloc byte[16];
        aes.DecryptCbc(ciphertext, iv, destination, PaddingMode.None);
    }

    /// <inheritdoc />
    public void Decrypt(ReadOnlySpan<byte> ciphertext, Span<byte> destination) =>
        Decrypt(_aes, ciphertext, destination);

    /// <inheritdoc />
    public void Dispose() => _aes.Dispose();
}