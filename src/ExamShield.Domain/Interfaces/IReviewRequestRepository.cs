using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IReviewRequestRepository
{
    Task AddAsync(ReviewRequest request, CancellationToken ct = default);
    Task<ReviewRequest?> GetByIdAsync(ReviewRequestId id, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewRequest>> ListByStudentAsync(StudentId studentId, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewRequest>> ListByCaptureIdsAsync(IReadOnlyList<CaptureId> captureIds, CancellationToken ct = default);
}
