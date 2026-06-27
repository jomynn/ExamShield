using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryReviewRequestRepository : IReviewRequestRepository
{
    private readonly ConcurrentDictionary<ReviewRequestId, ReviewRequest> _store = new();

    public Task AddAsync(ReviewRequest request, CancellationToken ct = default)
    {
        _store[request.Id] = request;
        return Task.CompletedTask;
    }

    public Task<ReviewRequest?> GetByIdAsync(ReviewRequestId id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var r) ? r : null);

    public Task UpdateAsync(ReviewRequest request, CancellationToken ct = default)
    {
        _store[request.Id] = request;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReviewRequest>> ListAllAsync(
        ReviewRequestStatus? status = null, CancellationToken ct = default)
    {
        var items = _store.Values.AsEnumerable();
        if (status.HasValue)
            items = items.Where(r => r.Status == status.Value);
        return Task.FromResult<IReadOnlyList<ReviewRequest>>(
            items.OrderByDescending(r => r.CreatedAt).ToList());
    }

    public Task<IReadOnlyList<ReviewRequest>> ListByStudentAsync(
        StudentId studentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ReviewRequest>>(
            _store.Values
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList());

    public Task<IReadOnlyList<ReviewRequest>> ListByCaptureIdsAsync(
        IReadOnlyList<CaptureId> captureIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ReviewRequest>>(
            _store.Values.Where(r => captureIds.Contains(r.CaptureId)).ToList());
}
