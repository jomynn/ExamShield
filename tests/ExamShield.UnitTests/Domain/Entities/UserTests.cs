using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class UserTests
{
    private static readonly Email TestEmail = new("admin@examshield.io");
    private const string TestHash = "$2a$04$hash";

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Administrator);

        user.Email.Should().Be(TestEmail);
        user.Role.Should().Be(UserRole.Administrator);
        user.IsActive.Should().BeTrue();
        user.Id.Value.Should().NotBe(Guid.Empty);
        user.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullEmail_Throws()
    {
        var act = () => User.Create(null!, TestHash, UserRole.Operator);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOrEmptyHash_Throws()
    {
        var act = () => User.Create(TestEmail, null!, UserRole.Operator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_StoresPasswordHashVerbatim()
    {
        const string hash = "$2a$04$abcdefghijklmnopqrstuuVGmFbkFDNVLBr0E5GjBnY8zf3wCe1Q2";
        var user = User.Create(TestEmail, hash, UserRole.Supervisor);
        user.PasswordHash.Should().Be(hash);
    }

    // ── Lockout ───────────────────────────────────────────────────────────────

    [Fact]
    public void RecordFailedLogin_BelowThreshold_DoesNotLock()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.RecordFailedLogin(maxAttempts: 3, lockoutDuration: TimeSpan.FromMinutes(15));
        user.IsLockedOut.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public void RecordFailedLogin_AtThreshold_LocksUser()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        for (var i = 0; i < 3; i++)
            user.RecordFailedLogin(maxAttempts: 3, lockoutDuration: TimeSpan.FromMinutes(15));

        user.IsLockedOut.Should().BeTrue();
        user.LockedUntil.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ResetFailedLogin_ClearsCounterAndLockout()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.RecordFailedLogin(3, TimeSpan.FromMinutes(15));
        user.RecordFailedLogin(3, TimeSpan.FromMinutes(15));

        user.ResetFailedLogin();

        user.FailedLoginAttempts.Should().Be(0);
        user.IsLockedOut.Should().BeFalse();
        user.LockedUntil.Should().BeNull();
    }

    // ── Deactivate / Reactivate ───────────────────────────────────────────────

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.Deactivate();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_InactiveUser_SetsIsActiveTrue()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.Deactivate();
        user.Reactivate();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Reactivate_AlreadyActive_ThrowsInvalidOperation()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.Invoking(u => u.Reactivate()).Should().Throw<InvalidOperationException>().WithMessage("*already active*");
    }

    // ── ChangePassword ────────────────────────────────────────────────────────

    [Fact]
    public void ChangePassword_ValidHash_UpdatesPasswordHash()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.ChangePassword("new-hash-value");
        user.PasswordHash.Should().Be("new-hash-value");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePassword_EmptyHash_ThrowsArgumentException(string hash)
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        var act = () => user.ChangePassword(hash);
        act.Should().Throw<ArgumentException>();
    }

    // ── UpdateProfile ─────────────────────────────────────────────────────────

    [Fact]
    public void UpdateProfile_ValidName_SetsDisplayName()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.UpdateProfile("  Alice Smith  ");
        user.DisplayName.Should().Be("Alice Smith");
    }

    [Fact]
    public void UpdateProfile_NullDisplayName_ClearsIt()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.UpdateProfile("Alice");
        user.UpdateProfile(null);
        user.DisplayName.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateProfile_WhitespaceOnlyName_ThrowsArgumentException(string name)
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        var act = () => user.UpdateProfile(name);
        act.Should().Throw<ArgumentException>().WithMessage("*empty or whitespace*");
    }

    [Fact]
    public void UpdateProfile_NameOver100Chars_ThrowsArgumentOutOfRange()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        var act = () => user.UpdateProfile(new string('x', 101));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── MFA ───────────────────────────────────────────────────────────────────

    [Fact]
    public void SetMfaSecret_StoresSecret()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.SetMfaSecret("TOTP123");
        user.MfaSecret.Should().Be("TOTP123");
    }

    [Fact]
    public void EnableMfa_SetsMfaEnabledTrue()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.EnableMfa();
        user.MfaEnabled.Should().BeTrue();
    }

    [Fact]
    public void DisableMfa_ClearsMfaEnabledAndSecret()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.SetMfaSecret("SECRET");
        user.EnableMfa();
        user.DisableMfa();

        user.MfaEnabled.Should().BeFalse();
        user.MfaSecret.Should().BeNull();
    }

    // ── ChangeRole ────────────────────────────────────────────────────────────

    [Fact]
    public void ChangeRole_UpdatesRole()
    {
        var user = User.Create(TestEmail, TestHash, UserRole.Operator);
        user.ChangeRole(UserRole.Administrator);
        user.Role.Should().Be(UserRole.Administrator);
    }
}
