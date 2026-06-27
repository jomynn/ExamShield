using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryCaptureRepository : ICaptureRepository
{
    private readonly ConcurrentDictionary<CaptureId, Capture> _store = new();

    public Task AddAsync(Capture capture, CancellationToken ct = default)
    {
        _store[capture.Id] = capture;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Capture capture, CancellationToken ct = default)
    {
        _store[capture.Id] = capture;
        return Task.CompletedTask;
    }

    public Task<Capture?> GetByIdAsync(CaptureId id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var capture) ? capture : null);

    public Task<IReadOnlyList<Capture>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Capture>>(
            _store.Values.OrderByDescending(c => c.CapturedAt).ToList());

    public Task<IReadOnlyList<Capture>> ListByStatusAsync(CaptureStatus status, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Capture>>(_store.Values.Where(c => c.Status == status).ToList());

    public Task<IReadOnlyList<Capture>> ListByStudentIdAsync(StudentId studentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Capture>>(
            _store.Values.Where(c => c.StudentId == studentId).OrderByDescending(c => c.CapturedAt).ToList());

    public Task<(IReadOnlyList<Capture> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var all = _store.Values.OrderByDescending(c => c.CapturedAt).ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Capture>, int)>((items, all.Count));
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        Task.FromResult(_store.Count);

    public Task<int> CountVerifiedSinceAsync(DateTimeOffset since, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.Count(c => c.Status == CaptureStatus.Verified && c.CapturedAt >= since));

    public Task<IReadOnlyList<Capture>> ListByExamIdAsync(ExamId examId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Capture>>(
            _store.Values.Where(c => c.ExamId == examId).ToList());
}
