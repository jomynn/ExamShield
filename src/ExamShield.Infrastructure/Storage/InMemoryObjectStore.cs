using System.Collections.Concurrent;

namespace ExamShield.Infrastructure.Storage;

public sealed class InMemoryObjectStore : IObjectStore
{
    private readonly ConcurrentDictionary<string, byte[]> _data = new();

    public int Count => _data.Count;
    public IEnumerable<string> Keys => _data.Keys;
    public IEnumerable<byte[]> Values => _data.Values;

    public Task PutAsync(string key, byte[] data, CancellationToken ct)
    {
        _data[key] = data;
        return Task.CompletedTask;
    }

    public Task<byte[]> GetAsync(string key, CancellationToken ct) =>
        Task.FromResult(_data.TryGetValue(key, out var v) ? v : Array.Empty<byte>());
}
