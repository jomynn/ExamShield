using ExamShield.Domain.Entities;
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
