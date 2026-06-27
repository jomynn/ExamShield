using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class ManualReviewRepository : IManualReviewRepository
{
    private readonly ExamShieldDbContext _context;

    public ManualReviewRepository(ExamShieldDbContext context) => _context = context;

    public async Task AddAsync(ManualReview review, CancellationToken ct = default)
    {
        await _context.ManualReviews.AddAsync(review, ct);
        await _context.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<ManualReview>> GetPendingAsync(CancellationToken ct = default) =>
        _context.ManualReviews
            .Where(r => r.Status == ManualReviewStatus.Pending)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ManualReview>)t.Result, ct);

    public Task<ManualReview?> GetByIdAsync(ManualReviewId id, CancellationToken ct = default) =>
        _context.ManualReviews
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<ManualReview?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default) =>
        _context.ManualReviews
            .FirstOrDefaultAsync(r => r.CaptureId == captureId, ct);

    public async Task UpdateAsync(ManualReview review, CancellationToken ct = default)
    {
        _context.ManualReviews.Update(review);
        await _context.SaveChangesAsync(ct);
    }
}
