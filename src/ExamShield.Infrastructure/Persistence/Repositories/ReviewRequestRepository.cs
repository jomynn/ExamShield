using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class ReviewRequestRepository(ExamShieldDbContext context) : IReviewRequestRepository
{
    public async Task AddAsync(ReviewRequest request, CancellationToken ct = default)
    {
        await context.ReviewRequests.AddAsync(request, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task<ReviewRequest?> GetByIdAsync(ReviewRequestId id, CancellationToken ct = default) =>
        context.ReviewRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task UpdateAsync(ReviewRequest request, CancellationToken ct = default)
    {
        context.ReviewRequests.Update(request);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReviewRequest>> ListAllAsync(
        ReviewRequestStatus? status = null, CancellationToken ct = default)
    {
        var query = context.ReviewRequests.AsQueryable();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    public Task<IReadOnlyList<ReviewRequest>> ListByStudentAsync(
        StudentId studentId, CancellationToken ct = default) =>
        context.ReviewRequests
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ReviewRequest>)t.Result, ct);

    public async Task<IReadOnlyList<ReviewRequest>> ListByCaptureIdsAsync(
        IReadOnlyList<CaptureId> captureIds, CancellationToken ct = default)
    {
        var ids = new HashSet<Guid>(captureIds.Select(c => c.Value));
        return await context.ReviewRequests.Where(r => ids.Contains(r.CaptureId.Value)).ToListAsync(ct);
    }
}
