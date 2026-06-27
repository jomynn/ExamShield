using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class StubAnswerKeyRepository : IAnswerKeyRepository
{
    // Returns a fixed answer key matching the StubOcrService outputs (Q1=A, Q2=B, Q3=C).
    public Task<AnswerKey?> GetByExamIdAsync(ExamId examId, CancellationToken ct = default)
    {
        var key = new AnswerKey(new Dictionary<int, string>
        {
            [1] = "A",
            [2] = "B",
            [3] = "C"
        });
        return Task.FromResult<AnswerKey?>(key);
    }

    public Task<ExamAnswerKey?> GetEntityByExamIdAsync(ExamId examId, CancellationToken ct = default) =>
        Task.FromResult<ExamAnswerKey?>(null);

    public Task SaveAsync(ExamAnswerKey key, CancellationToken ct = default) => Task.CompletedTask;
}
