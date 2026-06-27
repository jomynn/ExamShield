namespace ExamShield.Domain.ValueObjects;

public sealed class Password
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public string Value { get; }

    public Password(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Password cannot be empty.", nameof(value));
        if (value.Length < MinLength)
            throw new ArgumentException($"Password must be at least {MinLength} characters.", nameof(value));
        if (value.Length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"Password cannot exceed {MaxLength} characters.");
        if (!value.Any(char.IsUpper))
            throw new ArgumentException("Password must contain at least one uppercase letter.", nameof(value));
        if (!value.Any(char.IsLower))
            throw new ArgumentException("Password must contain at least one lowercase letter.", nameof(value));
        if (!value.Any(char.IsDigit))
            throw new ArgumentException("Password must contain at least one digit.", nameof(value));
        if (!value.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Password must contain at least one special character.", nameof(value));
        Value = value;
    }
}
