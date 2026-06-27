using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IManualReviewRepository
{
    Task AddAsync(ManualReview review, CancellationToken ct = default);
    Task<IReadOnlyList<ManualReview>> GetPendingAsync(CancellationToken ct = default);
    Task<ManualReview?> GetByIdAsync(ManualReviewId id, CancellationToken ct = default);
    Task<ManualReview?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default);
    Task UpdateAsync(ManualReview review, CancellationToken ct = default);
}
