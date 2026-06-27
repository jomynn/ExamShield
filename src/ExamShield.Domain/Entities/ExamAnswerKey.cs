using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Entities;

public sealed class ExamAnswerKey : AggregateRoot
{
    public ExamId ExamId { get; private set; } = null!;
    public IReadOnlyDictionary<int, string> Answers { get; private set; } = new Dictionary<int, string>();
    public DateTimeOffset CreatedAt { get; private set; }

    private ExamAnswerKey() { }

    public static ExamAnswerKey Create(ExamId examId, IReadOnlyDictionary<int, string> answers)
    {
        ArgumentNullException.ThrowIfNull(examId);
        if (answers is null || answers.Count == 0)
            throw new ArgumentException("Answer key must contain at least one answer.", nameof(answers));

        return new ExamAnswerKey
        {
            ExamId = examId,
            Answers = new Dictionary<int, string>(answers),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public AnswerKey ToValueObject() => new(Answers);
}
