using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ConcurrentDictionary<string, RefreshToken> _store = new();

    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _store[token.TokenHash] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct = default)
    {
        _store.TryGetValue(hash, out var token);
        return Task.FromResult(token);
    }

    public Task<RefreshToken?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(UserId userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            _store.Values.Where(t => t.UserId == userId && t.IsActive).ToList());

    public Task SaveAsync(RefreshToken token, CancellationToken ct = default)
    {
        _store[token.TokenHash] = token;
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(UserId userId, CancellationToken ct = default)
    {
        foreach (var kv in _store.Where(kv => kv.Value.UserId == userId))
            kv.Value.Revoke();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RefreshToken>> ListAllActiveAsync(UserId? userId, CancellationToken ct = default)
    {
        var query = _store.Values.Where(t => t.IsActive);
        if (userId is not null)
            query = query.Where(t => t.UserId == userId);
        return Task.FromResult<IReadOnlyList<RefreshToken>>(query.ToList());
    }
}
