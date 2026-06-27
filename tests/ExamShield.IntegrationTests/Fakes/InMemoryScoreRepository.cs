using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryScoreRepository : IScoreRepository
{
    private readonly ConcurrentDictionary<Guid, Score> _store = new();

    public Task AddAsync(Score score, CancellationToken ct = default)
    {
        _store[score.Id.Value] = score;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Score score, CancellationToken ct = default)
    {
        _store[score.Id.Value] = score;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Score>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Score>>(_store.Values.ToList());

    public Task<IReadOnlyList<Score>> GetPublishedAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Score>>(_store.Values.Where(s => s.IsPublished).ToList());

    public Task<IReadOnlyList<Score>> GetByExamIdAsync(ExamId examId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Score>>(_store.Values.Where(s => s.ExamId == examId).ToList());

    public Task<bool> ExistsByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.Any(s => s.CaptureId == captureId));

    public Task<Score?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(s => s.CaptureId == captureId));
}
