using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class ScoreRepository : IScoreRepository
{
    private readonly ExamShieldDbContext _context;

    public ScoreRepository(ExamShieldDbContext context) => _context = context;

    public async Task AddAsync(Score score, CancellationToken ct = default)
    {
        await _context.Scores.AddAsync(score, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Score score, CancellationToken ct = default)
    {
        _context.Scores.Update(score);
        await _context.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<Score>> GetAllAsync(CancellationToken ct = default) =>
        _context.Scores.ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Score>)t.Result, ct);

    public Task<IReadOnlyList<Score>> GetPublishedAsync(CancellationToken ct = default) =>
        _context.Scores.Where(s => s.IsPublished).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Score>)t.Result, ct);

    public Task<IReadOnlyList<Score>> GetByExamIdAsync(ExamId examId, CancellationToken ct = default) =>
        _context.Scores.Where(s => s.ExamId == examId).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Score>)t.Result, ct);

    public Task<bool> ExistsByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default) =>
        _context.Scores.AnyAsync(s => s.CaptureId == captureId, ct);

    public Task<Score?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default) =>
        _context.Scores.FirstOrDefaultAsync(s => s.CaptureId == captureId, ct);
}
