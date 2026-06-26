using System.Text.Json;
using ExamShield.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace ExamShield.Infrastructure.Cache;

public sealed class CacheService(IDistributedCache inner) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await inner.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await inner.SetAsync(key, bytes, opts, ct);
    }

    public Task InvalidateAsync(string key, CancellationToken ct = default) =>
        inner.RemoveAsync(key, ct);
}
