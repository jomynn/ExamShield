using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryExamRepository : IExamRepository
{
    private readonly ConcurrentDictionary<ExamId, Exam> _store = new();

    public InMemoryExamRepository(IEnumerable<Exam>? seed = null)
    {
        foreach (var exam in seed ?? [])
            _store[exam.Id] = exam;
    }

    public Task AddAsync(Exam exam, CancellationToken ct = default)
    {
        _store[exam.Id] = exam;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Exam exam, CancellationToken ct = default)
    {
        _store[exam.Id] = exam;
        return Task.CompletedTask;
    }

    public Task<Exam?> GetByIdAsync(ExamId id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var exam) && !exam.IsDeleted ? exam : null);

    public Task<IReadOnlyList<Exam>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Exam>>(_store.Values.Where(e => !e.IsDeleted).ToList());

    public Task<(IReadOnlyList<Exam> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize,
        string? search = null, ExamStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _store.Values.Where(e => !e.IsDeleted).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (status is not null)
            query = query.Where(e => e.Status == status);
        var all = query.OrderByDescending(e => e.CreatedAt).ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Exam>, int)>((items, all.Count));
    }
}
