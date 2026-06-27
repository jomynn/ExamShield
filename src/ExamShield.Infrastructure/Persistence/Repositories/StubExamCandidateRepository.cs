using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class StubExamCandidateRepository : IExamCandidateRepository
{
    public Task AddAsync(ExamCandidate candidate, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<ExamCandidate>> ListByExamIdAsync(ExamId examId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExamCandidate>>(Array.Empty<ExamCandidate>());

    public Task<bool> ExistsAsync(ExamId examId, StudentId studentId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task RemoveAsync(ExamId examId, StudentId studentId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
