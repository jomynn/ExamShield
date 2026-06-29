using System.Text;
using ExamShield.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace ExamShield.Infrastructure.Security;

/// <summary>
/// Redis-backed TOTP replay-prevention cache.
/// Each used code is stored with a 3-minute TTL (two 30-second TOTP windows either side
/// of the current one, matching TOTP's standard ±1 window tolerance), so the entry
/// expires automatically and the key space stays small.
/// This implementation is safe for multi-replica deployments — InMemoryTotpUsedCodeCache
/// is not, because codes used on replica A are invisible to replica B.
/// </summary>
public sealed class RedisTotpUsedCodeCache(IDistributedCache cache) : ITotpUsedCodeCache
{
    private static readonly DistributedCacheEntryOptions Ttl =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3) };

    public async Task<bool> IsUsedAsync(string userId, string code, CancellationToken ct = default)
    {
        var val = await cache.GetAsync(Key(userId, code), ct);
        return val is not null;
    }

    public Task MarkUsedAsync(string userId, string code, CancellationToken ct = default) =>
        cache.SetAsync(Key(userId, code), [1], Ttl, ct);

    private static string Key(string userId, string code) => $"totp:{userId}:{code}";
}
