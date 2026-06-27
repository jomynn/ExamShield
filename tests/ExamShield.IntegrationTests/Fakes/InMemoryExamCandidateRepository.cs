using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryExamCandidateRepository : IExamCandidateRepository
{
    private readonly ConcurrentDictionary<(Guid ExamId, Guid StudentId), ExamCandidate> _store = new();

    public Task AddAsync(ExamCandidate candidate, CancellationToken ct = default)
    {
        _store.TryAdd((candidate.ExamId.Value, candidate.StudentId.Value), candidate);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExamCandidate>> ListByExamIdAsync(ExamId examId, CancellationToken ct = default)
    {
        var result = _store.Values.Where(c => c.ExamId == examId).ToList();
        return Task.FromResult<IReadOnlyList<ExamCandidate>>(result);
    }

    public Task<bool> ExistsAsync(ExamId examId, StudentId studentId, CancellationToken ct = default)
    {
        var exists = _store.ContainsKey((examId.Value, studentId.Value));
        return Task.FromResult(exists);
    }

    public Task<int> CountByExamIdAsync(ExamId examId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.Count(c => c.ExamId == examId));

    public Task RemoveAsync(ExamId examId, StudentId studentId, CancellationToken ct = default)
    {
        _store.TryRemove((examId.Value, studentId.Value), out _);
        return Task.CompletedTask;
    }
}
