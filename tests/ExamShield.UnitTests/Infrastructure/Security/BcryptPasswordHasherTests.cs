using ExamShield.Infrastructure.Security;
using FluentAssertions;

namespace ExamShield.UnitTests.Infrastructure.Security;

public sealed class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut = new();

    [Fact]
    public void Hash_ProducesNonEmptyString()
    {
        var hash = _sut.Hash("my-password");
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_DifferentCallsSamePlaintext_ProduceDifferentHashes()
    {
        var h1 = _sut.Hash("same-password");
        var h2 = _sut.Hash("same-password");
        h1.Should().NotBe(h2); // bcrypt uses random salt
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _sut.Hash("correct-horse-battery-staple");
        _sut.Verify("correct-horse-battery-staple", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("right-password");
        _sut.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyPlaintext_AgainstHashOfEmpty_ReturnsTrue()
    {
        var hash = _sut.Hash(string.Empty);
        _sut.Verify(string.Empty, hash).Should().BeTrue();
    }

    [Fact]
    public void Hash_OutputStartsWithBcryptPrefix()
    {
        var hash = _sut.Hash("password");
        hash.Should().StartWith("$2");
    }
}
