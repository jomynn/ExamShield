using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface ICaptureRepository
{
    Task AddAsync(Capture capture, CancellationToken ct = default);
    Task UpdateAsync(Capture capture, CancellationToken ct = default);
    Task<Capture?> GetByIdAsync(CaptureId id, CancellationToken ct = default);
    Task<IReadOnlyList<Capture>> ListAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<Capture> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize,
        ExamId? examId = null, CaptureStatus? status = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<Capture>> ListByStatusAsync(CaptureStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Capture>> ListByStudentIdAsync(StudentId studentId, CancellationToken ct = default);
    Task<IReadOnlyList<Capture>> ListByExamIdAsync(ExamId examId, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountVerifiedSinceAsync(DateTimeOffset since, CancellationToken ct = default);
}
