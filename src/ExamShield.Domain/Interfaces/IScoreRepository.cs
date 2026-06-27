using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IScoreRepository
{
    Task AddAsync(Score score, CancellationToken ct = default);
    Task UpdateAsync(Score score, CancellationToken ct = default);
    Task<IReadOnlyList<Score>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Score>> GetPublishedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Score>> GetByExamIdAsync(ExamId examId, CancellationToken ct = default);
    Task<bool> ExistsByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default);
    Task<Score?> GetByCaptureIdAsync(CaptureId captureId, CancellationToken ct = default);
}
