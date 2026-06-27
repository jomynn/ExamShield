using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.ValueObjects;

public sealed class PasswordTests
{
    [Theory]
    [InlineData("P@ssword1")]          // valid
    [InlineData("Abcdef1!")]           // exactly 8 chars
    [InlineData("MyStr0ng#Password")]  // valid long
    public void Constructor_ValidPassword_Succeeds(string value)
    {
        var act = () => new Password(value);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_TooShort_Throws()
    {
        var act = () => new Password("Ab1!");
        act.Should().Throw<ArgumentException>()
           .WithMessage("*at least 8*");
    }

    [Fact]
    public void Constructor_TooLong_Throws()
    {
        var act = () => new Password(new string('A', 126) + "1!a");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NoUppercase_Throws()
    {
        var act = () => new Password("p@ssword1");
        act.Should().Throw<ArgumentException>()
           .WithMessage("*uppercase*");
    }

    [Fact]
    public void Constructor_NoLowercase_Throws()
    {
        var act = () => new Password("P@SSWORD1");
        act.Should().Throw<ArgumentException>()
           .WithMessage("*lowercase*");
    }

    [Fact]
    public void Constructor_NoDigit_Throws()
    {
        var act = () => new Password("P@ssword!");
        act.Should().Throw<ArgumentException>()
           .WithMessage("*digit*");
    }

    [Fact]
    public void Constructor_NoSpecialChar_Throws()
    {
        var act = () => new Password("Passw0rd");
        act.Should().Throw<ArgumentException>()
           .WithMessage("*special*");
    }

    [Fact]
    public void Constructor_Empty_Throws()
    {
        var act = () => new Password("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Value_ReturnsOriginalString()
    {
        const string raw = "P@ssword1";
        new Password(raw).Value.Should().Be(raw);
    }
}
