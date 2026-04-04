using System.Security.Cryptography;

namespace AAPakFile.Core;

public class EncryptorTests
{
    // Mirrors the FakeAes pattern in DecryptorTests, but overrides TryEncryptCbcCore.
    private class FakeAes : Aes
    {
        private readonly List<(byte[] Plaintext, byte[] Iv, PaddingMode PaddingMode)> _calls = [];

        public IReadOnlyList<(byte[] Plaintext, byte[] Iv, PaddingMode PaddingMode)> Calls => _calls;

        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[]? rgbIv) =>
            throw new NotImplementedException();

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[]? rgbIv) =>
            throw new NotImplementedException();

        public override void GenerateIV() => throw new NotImplementedException();

        public override void GenerateKey() => throw new NotImplementedException();

        protected override bool TryEncryptCbcCore(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> iv,
            Span<byte> destination, PaddingMode paddingMode, out int bytesWritten)
        {
            _calls.Add((plaintext.ToArray(), iv.ToArray(), paddingMode));
            plaintext.CopyTo(destination);
            bytesWritten = plaintext.Length;
            return true;
        }
    }

    [Test]
    public async Task Encrypt_StaticMethod_CallsAesInCbcMode_WithEmptyIv_AndNoPadding()
    {
        var plaintext = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var destination = new byte[plaintext.Length];
        var expectedIv = new byte[16];

        var aesFake = new FakeAes();

        Encryptor.Encrypt(aesFake, plaintext, destination);

        var call = await Assert.That(aesFake.Calls).HasSingleItem();
        await Assert.That(call.Plaintext).IsSequenceEqualTo(plaintext);
        await Assert.That(call.Iv).IsSequenceEqualTo(expectedIv);
        await Assert.That(call.PaddingMode).IsEqualTo(PaddingMode.None);
    }

    [Test]
    public async Task Encrypt_InstanceMethod_DelegatesToStaticMethod()
    {
        var plaintext = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var destination = new byte[plaintext.Length];
        var expectedIv = new byte[16];

        var aesFake = new FakeAes();
        var cut = new Encryptor(aesFake);

        cut.Encrypt(plaintext, destination);

        var call = await Assert.That(aesFake.Calls).HasSingleItem();
        await Assert.That(call.Plaintext).IsSequenceEqualTo(plaintext);
        await Assert.That(call.Iv).IsSequenceEqualTo(expectedIv);
        await Assert.That(call.PaddingMode).IsEqualTo(PaddingMode.None);
    }
}