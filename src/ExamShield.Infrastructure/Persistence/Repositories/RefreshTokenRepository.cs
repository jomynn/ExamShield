using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(ExamShieldDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        await db.RefreshTokens.AddAsync(token, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

    public Task<RefreshToken?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(UserId userId, CancellationToken ct = default) =>
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);

    public async Task SaveAsync(RefreshToken token, CancellationToken ct = default)
    {
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListAllActiveAsync(UserId? userId, CancellationToken ct = default)
    {
        var query = db.RefreshTokens
            .Where(t => t.RevokedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow);
        if (userId is not null)
            query = query.Where(t => t.UserId == userId);
        return await query.ToListAsync(ct);
    }

    public async Task RevokeAllForUserAsync(UserId userId, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.Revoke();
        await db.SaveChangesAsync(ct);
    }
}
