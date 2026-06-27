using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryExamCandidateRepository : IExamCandidateRepository
{
    private readonly ConcurrentBag<ExamCandidate> _store = new();

    public Task AddAsync(ExamCandidate candidate, CancellationToken ct = default)
    {
        _store.Add(candidate);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExamCandidate>> ListByExamIdAsync(ExamId examId, CancellationToken ct = default)
    {
        var result = _store.Where(c => c.ExamId == examId).ToList();
        return Task.FromResult<IReadOnlyList<ExamCandidate>>(result);
    }

    public Task<bool> ExistsAsync(ExamId examId, StudentId studentId, CancellationToken ct = default)
    {
        var exists = _store.Any(c => c.ExamId == examId && c.StudentId == studentId);
        return Task.FromResult(exists);
    }
}
