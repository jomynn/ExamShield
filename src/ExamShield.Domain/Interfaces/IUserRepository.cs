using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveAsync(User user, CancellationToken ct = default);
    Task<User?> FindByEmailAsync(Email email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize,
        string? search = null, string? role = null,
        CancellationToken ct = default);
}
