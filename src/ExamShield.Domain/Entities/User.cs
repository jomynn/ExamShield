using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Entities;

public sealed class User
{
    public UserId Id { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { } // EF Core

    public static User Create(Email email, string passwordHash, UserRole role)
    {
        ArgumentNullException.ThrowIfNull(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        return new User
        {
            Id = UserId.New(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public bool MfaEnabled { get; private set; }
    public string? MfaSecret { get; private set; }
    public string? DisplayName { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    public bool IsLockedOut =>
        LockedUntil is not null && LockedUntil > DateTimeOffset.UtcNow;

    public void RecordFailedLogin(int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
            LockedUntil = DateTimeOffset.UtcNow.Add(lockoutDuration);
    }

    public void ResetFailedLogin()
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public void UpdateProfile(string? displayName)
    {
        if (displayName is not null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name cannot be empty or whitespace.", nameof(displayName));
            if (displayName.Length > 100)
                throw new ArgumentOutOfRangeException(nameof(displayName), "Display name cannot exceed 100 characters.");
            DisplayName = displayName.Trim();
        }
        else
        {
            DisplayName = null;
        }
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate()
    {
        if (IsActive)
            throw new InvalidOperationException("User is already active.");
        IsActive = true;
    }

    public void ChangePassword(string newHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash, nameof(newHash));
        PasswordHash = newHash;
    }

    public void ChangeRole(UserRole newRole) => Role = newRole;

    public void SetMfaSecret(string secret) => MfaSecret = secret;

    public void EnableMfa() => MfaEnabled = true;

    public void DisableMfa()
    {
        MfaEnabled = false;
        MfaSecret = null;
    }
}
