using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Entities;

public enum ExamStatus { Draft, Active, Closed }

public sealed class Exam : AggregateRoot
{
    public ExamId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ExamStatus Status { get; private set; }
    public int TotalQuestions { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }

    private Exam() { }

    public static Exam Create(
        string name, string? description, int totalQuestions,
        DateTimeOffset? scheduledAt = null, DateTimeOffset? endsAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        if (totalQuestions <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalQuestions), "Must be greater than zero.");
        if (scheduledAt.HasValue && endsAt.HasValue && endsAt <= scheduledAt)
            throw new ArgumentException("EndsAt must be after ScheduledAt.", nameof(endsAt));

        return new Exam
        {
            Id = ExamId.New(), Name = name, Description = description,
            TotalQuestions = totalQuestions, CreatedAt = DateTimeOffset.UtcNow,
            Status = ExamStatus.Draft,
            ScheduledAt = scheduledAt, EndsAt = endsAt
        };
    }

    public void Update(
        string name, string? description,
        DateTimeOffset? scheduledAt, DateTimeOffset? endsAt)
    {
        if (Status != ExamStatus.Draft)
            throw new InvalidOperationException("Only Draft exams can be updated.");
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        if (scheduledAt.HasValue && endsAt.HasValue && endsAt <= scheduledAt)
            throw new ArgumentException("EndsAt must be after ScheduledAt.", nameof(endsAt));

        Name        = name.Trim();
        Description = description;
        ScheduledAt = scheduledAt;
        EndsAt      = endsAt;
    }

    public void Activate()
    {
        if (Status != ExamStatus.Draft)
            throw new InvalidOperationException("Only Draft exams can be activated.");
        Status = ExamStatus.Active;
    }

    public void Close()
    {
        if (Status == ExamStatus.Closed)
            throw new InvalidOperationException("Exam is already closed.");
        Status = ExamStatus.Closed;
    }
}
