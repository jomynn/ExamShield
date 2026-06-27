using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.UnitTests.Domain;

public sealed class UserLockoutTests
{
    private static User MakeUser() =>
        User.Create(new Email("test@test.com"), "hash", UserRole.Operator);

    [Fact]
    public void RecordFailedLogin_BelowThreshold_NotLocked()
    {
        var user = MakeUser();
        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        Assert.False(user.IsLockedOut);
    }

    [Fact]
    public void RecordFailedLogin_AtThreshold_LocksAccount()
    {
        var user = MakeUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        Assert.True(user.IsLockedOut);
    }

    [Fact]
    public void RecordFailedLogin_LocksForSpecifiedDuration()
    {
        var user = MakeUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        Assert.NotNull(user.LockedUntil);
        Assert.True(user.LockedUntil > DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public void ResetFailedLogin_ClearsAttemptsAndLockout()
    {
        var user = MakeUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        Assert.True(user.IsLockedOut);

        user.ResetFailedLogin();

        Assert.False(user.IsLockedOut);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void IsLockedOut_WhenLockedUntilInFuture_ReturnsTrue()
    {
        var user = MakeUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        Assert.True(user.IsLockedOut);
    }
}
