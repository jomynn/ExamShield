using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IOcrResultRepository
{
    Task AddAsync(OcrResult result, CancellationToken ct = default);
    Task<OcrResult?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default);
    Task<OcrResult?> GetByIdAsync(OcrResultId id, CancellationToken ct = default);
    Task<IReadOnlyList<OcrResult>> ListCompletedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OcrResult>> ListByCaptureIdsAsync(IReadOnlyList<CaptureId> captureIds, CancellationToken ct = default);
}
