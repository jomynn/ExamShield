using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class ExamRepository(ExamShieldDbContext context) : IExamRepository
{
    public async Task AddAsync(Exam exam, CancellationToken ct = default)
    {
        await context.Exams.AddAsync(exam, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Exam exam, CancellationToken ct = default)
    {
        context.Exams.Update(exam);
        await context.SaveChangesAsync(ct);
    }

    public Task<Exam?> GetByIdAsync(ExamId id, CancellationToken ct = default) =>
        context.Exams.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Exam>> ListAllAsync(CancellationToken ct = default) =>
        await context.Exams.ToListAsync(ct);

    public async Task<(IReadOnlyList<Exam> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize,
        string? search = null, ExamStatus? status = null,
        DateTimeOffset? scheduledFrom = null, DateTimeOffset? scheduledTo = null,
        CancellationToken ct = default)
    {
        var query = context.Exams.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.Name.Contains(search));
        if (status is not null)
            query = query.Where(e => e.Status == status);
        if (scheduledFrom is not null)
            query = query.Where(e => e.ScheduledAt != null && e.ScheduledAt >= scheduledFrom);
        if (scheduledTo is not null)
            query = query.Where(e => e.ScheduledAt != null && e.ScheduledAt <= scheduledTo);
        query = query.OrderByDescending(e => e.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
