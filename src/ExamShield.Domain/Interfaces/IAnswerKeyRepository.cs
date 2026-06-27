using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IAnswerKeyRepository
{
    Task<AnswerKey?> GetByExamIdAsync(ExamId examId, CancellationToken ct = default);
    Task<ExamAnswerKey?> GetEntityByExamIdAsync(ExamId examId, CancellationToken ct = default);
    Task SaveAsync(ExamAnswerKey key, CancellationToken ct = default);
}
