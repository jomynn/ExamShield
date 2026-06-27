using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class CaptureRepository : ICaptureRepository
{
    private readonly ExamShieldDbContext _context;

    public CaptureRepository(ExamShieldDbContext context) => _context = context;

    public async Task AddAsync(Capture capture, CancellationToken ct = default)
    {
        await _context.Captures.AddAsync(capture, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Capture capture, CancellationToken ct = default)
    {
        _context.Captures.Update(capture);
        await _context.SaveChangesAsync(ct);
    }

    public Task<Capture?> GetByIdAsync(CaptureId id, CancellationToken ct = default) =>
        _context.Captures.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Capture>> ListAllAsync(CancellationToken ct = default) =>
        await _context.Captures.OrderByDescending(c => c.CapturedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Capture>> ListByStatusAsync(CaptureStatus status, CancellationToken ct = default) =>
        await _context.Captures.Where(c => c.Status == status).ToListAsync(ct);

    public async Task<IReadOnlyList<Capture>> ListByStudentIdAsync(StudentId studentId, CancellationToken ct = default) =>
        await _context.Captures.Where(c => c.StudentId == studentId).OrderByDescending(c => c.CapturedAt).ToListAsync(ct);

    public async Task<(IReadOnlyList<Capture> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Captures.OrderByDescending(c => c.CapturedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Captures.CountAsync(ct);

    public Task<int> CountVerifiedSinceAsync(DateTimeOffset since, CancellationToken ct = default) =>
        _context.Captures.CountAsync(c => c.Status == CaptureStatus.Verified && c.CapturedAt >= since, ct);

    public async Task<IReadOnlyList<Capture>> ListByExamIdAsync(ExamId examId, CancellationToken ct = default) =>
        await _context.Captures.Where(c => c.ExamId == examId).ToListAsync(ct);
}
