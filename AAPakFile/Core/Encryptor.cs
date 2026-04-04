using System.Security.Cryptography;

namespace AAPakFile.Core;

/// <summary>
/// Provides an implementation for the encryption of package headers or file tables.
/// </summary>
internal sealed class Encryptor : IDisposable, IEncryptor
{
    private readonly Aes _aes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Encryptor"/> class with the given encryption key.
    /// </summary>
    /// <param name="key">The AES encryption key. If empty, <see cref="XlGamesAesKey.Default"/> is used.</param>
    /// <exception cref="CryptographicException">
    ///   <para>
    ///     The key size is invalid.
    ///   </para>
    ///   <para>-or-</para>
    ///   <para>
    ///     An error occurred while setting the key.
    ///   </para>
    /// </exception>
    public Encryptor(ReadOnlySpan<byte> key)
    {
        _aes = Aes.Create();

        if (key.IsEmpty)
        {
            key = XlGamesAesKey.Default.Span;
        }

        _aes.SetKey(key);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Encryptor"/> class with the given AES implementation.
    /// </summary>
    /// <param name="aes">The AES implementation to use for encryption.</param>
    public Encryptor(Aes aes) => _aes = aes;

    /// <summary>
    /// Encrypts the given plaintext representing a package header or file table entry, and stores the result in
    /// <paramref name="destination"/>.
    /// </summary>
    /// <param name="aes">An AES class that provides the encryption implementation, with the key already set.</param>
    /// <param name="plaintext">The span of bytes containing the plaintext data.</param>
    /// <param name="destination">
    /// A span at least as large as <paramref name="plaintext"/> to store the encrypted result in.
    /// </param>
    /// <exception cref="CryptographicException">The plaintext could not be encrypted successfully.</exception>
    /// <exception cref="ArgumentException">
    /// The buffer in <paramref name="destination"/> is too small to hold the ciphertext data.
    /// </exception>
    public static void Encrypt(Aes aes, ReadOnlySpan<byte> plaintext, Span<byte> destination)
    {
        Span<byte> iv = stackalloc byte[16];
        aes.EncryptCbc(plaintext, iv, destination, PaddingMode.None);
    }

    /// <inheritdoc />
    public void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> destination) =>
        Encrypt(_aes, plaintext, destination);

    /// <inheritdoc />
    public void Dispose() => _aes.Dispose();
}