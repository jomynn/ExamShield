using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class InMemoryManualReviewRepository : IManualReviewRepository
{
    private readonly ConcurrentDictionary<Guid, ManualReview> _store = new();

    public Task AddAsync(ManualReview review, CancellationToken ct = default)
    {
        _store[review.Id.Value] = review;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ManualReview>> GetPendingAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManualReview>>(
            _store.Values.Where(r => r.Status == ManualReviewStatus.Pending).ToList());

    public Task<ManualReview?> GetByIdAsync(ManualReviewId id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id.Value, out var review) ? review : null);

    public Task<ManualReview?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(r => r.CaptureId == captureId));

    public Task UpdateAsync(ManualReview review, CancellationToken ct = default)
    {
        _store[review.Id.Value] = review;
        return Task.CompletedTask;
    }
}
