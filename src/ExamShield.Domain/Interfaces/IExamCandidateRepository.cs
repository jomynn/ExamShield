using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IExamCandidateRepository
{
    Task AddAsync(ExamCandidate candidate, CancellationToken ct = default);
    Task<IReadOnlyList<ExamCandidate>> ListByExamIdAsync(ExamId examId, CancellationToken ct = default);
    Task<bool> ExistsAsync(ExamId examId, StudentId studentId, CancellationToken ct = default);
    Task<int> CountByExamIdAsync(ExamId examId, CancellationToken ct = default);
    Task RemoveAsync(ExamId examId, StudentId studentId, CancellationToken ct = default);
}
