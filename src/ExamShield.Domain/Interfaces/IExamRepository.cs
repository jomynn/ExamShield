using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IExamRepository
{
    Task AddAsync(Exam exam, CancellationToken ct = default);
    Task UpdateAsync(Exam exam, CancellationToken ct = default);
    Task<Exam?> GetByIdAsync(ExamId id, CancellationToken ct = default);
    Task<IReadOnlyList<Exam>> ListAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<Exam> Items, int TotalCount)> ListPagedAsync(int page, int pageSize, CancellationToken ct = default);
}
