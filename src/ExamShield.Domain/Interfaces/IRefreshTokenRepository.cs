using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct = default);
    Task<RefreshToken?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(UserId userId, CancellationToken ct = default);
    Task SaveAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllForUserAsync(UserId userId, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> ListAllActiveAsync(UserId? userId, CancellationToken ct = default);
}
