using System.Security.Cryptography;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Security;
using FluentAssertions;

namespace ExamShield.UnitTests.Infrastructure.Security;

public sealed class EcdsaSignatureVerificationServiceTests
{
    private readonly EcdsaSignatureVerificationService _sut = new();

    private static (Hash hash, Signature sig, PublicKey pubKey) MakeValidTriple()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var data = new byte[] { 1, 2, 3, 4 };
        var hash = Hash.FromBytes(SHA256.HashData(data));
        var sigBytes = ecdsa.SignHash(hash.ToBytes());
        var pubKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
        return (hash, new Signature(sigBytes), new PublicKey(pubKeyBytes));
    }

    [Fact]
    public void Verify_ValidHashAndSignature_ReturnsTrue()
    {
        var (hash, sig, pubKey) = MakeValidTriple();
        _sut.Verify(hash, sig, pubKey).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongHash_ReturnsFalse()
    {
        var (_, sig, pubKey) = MakeValidTriple();
        var differentHash = Hash.FromBytes(SHA256.HashData(new byte[] { 9, 9, 9 }));
        _sut.Verify(differentHash, sig, pubKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var (hash, sig, pubKey) = MakeValidTriple();
        var tampered = sig.Bytes.ToArray();
        tampered[0] ^= 0xFF;
        _sut.Verify(hash, new Signature(tampered), pubKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_InvalidPublicKeyBytes_ReturnsFalse()
    {
        var (hash, sig, _) = MakeValidTriple();
        var badKey = new PublicKey(new byte[64]); // invalid DER
        _sut.Verify(hash, sig, badKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_DifferentKeyPair_ReturnsFalse()
    {
        var (hash, sig, _) = MakeValidTriple();
        using var otherEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var otherPubKey = new PublicKey(otherEcdsa.ExportSubjectPublicKeyInfo());
        _sut.Verify(hash, sig, otherPubKey).Should().BeFalse();
    }
}
