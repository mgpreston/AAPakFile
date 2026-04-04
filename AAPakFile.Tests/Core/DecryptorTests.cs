using System.Security.Cryptography;

namespace AAPakFile.Core;

public class DecryptorTests
{
    // Due to the use of ref structs (ReadOnlySpan<T>), we can't mock using Moq, so use a test fake instead.
    private class FakeAes : Aes
    {
        private readonly List<(byte[] Ciphertext, byte[] Iv, PaddingMode PaddingMode)> _calls = [];

        public IReadOnlyList<(byte[] Ciphertext, byte[] Iv, PaddingMode PaddingMode)> Calls => _calls;

        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[]? rgbIv) =>
            throw new NotImplementedException();

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[]? rgbIv) =>
            throw new NotImplementedException();

        public override void GenerateIV() => throw new NotImplementedException();

        public override void GenerateKey() => throw new NotImplementedException();

        protected override bool TryDecryptCbcCore(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> iv,
            Span<byte> destination, PaddingMode paddingMode,
            out int bytesWritten)
        {
            _calls.Add((ciphertext.ToArray(), iv.ToArray(), paddingMode));
            ciphertext.CopyTo(destination);
            bytesWritten = ciphertext.Length;
            return true;
        }
    }

    [Test]
    public async Task Decrypt_CallsAesInCbcMode_WithEmptyIv_AndNoPadding()
    {
        var ciphertext = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var decryptedBuffer = new byte[ciphertext.Length];

        var iv = new byte[16];

        var aesFake = new FakeAes();
        var cut = new Decryptor(aesFake);

        cut.Decrypt(ciphertext, decryptedBuffer);

        var call = await Assert.That(aesFake.Calls).HasSingleItem();
        await Assert.That(call.Ciphertext).IsSequenceEqualTo(ciphertext);
        await Assert.That(call.Iv).IsSequenceEqualTo(iv);
        await Assert.That(call.PaddingMode).IsEqualTo(PaddingMode.None);
    }
}