using ExamShield.Infrastructure.Security;
using FluentAssertions;

namespace ExamShield.UnitTests.Infrastructure.Security;

public sealed class TotpServiceTests
{
    private readonly TotpService _sut = new();

    [Fact]
    public void GenerateSecret_ReturnsNonEmptyBase32String()
    {
        var secret = _sut.GenerateSecret();
        secret.Should().NotBeNullOrEmpty();
        secret.Should().MatchRegex("^[A-Z2-7]+=*$"); // base32 alphabet
    }

    [Fact]
    public void GenerateSecret_TwoCalls_ProduceDifferentSecrets()
    {
        var s1 = _sut.GenerateSecret();
        var s2 = _sut.GenerateSecret();
        s1.Should().NotBe(s2);
    }

    [Fact]
    public void GetQrUri_ContainsSecretAndIssuer()
    {
        var secret = _sut.GenerateSecret();
        var uri = _sut.GetQrUri(secret, "alice@exam.io");

        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain(secret);
        uri.Should().Contain("ExamShield");
    }

    [Fact]
    public void Verify_CurrentCode_ReturnsTrue()
    {
        var secret = _sut.GenerateSecret();
        var code = _sut.GenerateCurrentCode(secret);
        _sut.Verify(secret, code).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongCode_ReturnsFalse()
    {
        var secret = _sut.GenerateSecret();
        _sut.Verify(secret, "000000").Should().BeFalse();
    }

    [Fact]
    public void Verify_NonDigitCode_ReturnsFalse()
    {
        var secret = _sut.GenerateSecret();
        _sut.Verify(secret, "abcdef").Should().BeFalse();
    }

    [Fact]
    public void Verify_ShortCode_ReturnsFalse()
    {
        var secret = _sut.GenerateSecret();
        _sut.Verify(secret, "12345").Should().BeFalse();
    }

    [Fact]
    public void GenerateCurrentCode_Produces6DigitString()
    {
        var secret = _sut.GenerateSecret();
        var code = _sut.GenerateCurrentCode(secret);

        code.Should().HaveLength(6);
        code.Should().MatchRegex("^[0-9]{6}$");
    }
}
