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
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Exams.OrderByDescending(e => e.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
