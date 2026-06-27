using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryAnswerKeyRepository : IAnswerKeyRepository
{
    private readonly ConcurrentDictionary<Guid, ExamAnswerKey> _store = new();

    public Task<AnswerKey?> GetByExamIdAsync(ExamId examId, CancellationToken ct = default)
    {
        _store.TryGetValue(examId.Value, out var entity);
        var result = entity?.ToValueObject();
        return Task.FromResult(result);
    }

    public Task<ExamAnswerKey?> GetEntityByExamIdAsync(ExamId examId, CancellationToken ct = default)
    {
        _store.TryGetValue(examId.Value, out var entity);
        return Task.FromResult(entity);
    }

    public Task SaveAsync(ExamAnswerKey key, CancellationToken ct = default)
    {
        _store[key.ExamId.Value] = key;
        return Task.CompletedTask;
    }
}
