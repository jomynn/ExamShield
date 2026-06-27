using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly ConcurrentDictionary<DeviceId, Device> _store = new();

    public Task AddAsync(Device device, CancellationToken ct = default)
    {
        _store[device.Id] = device;
        return Task.CompletedTask;
    }

    public Task<Device?> GetByIdAsync(DeviceId id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var device) ? device : null);

    public Task<bool> ExistsByPublicKeyAsync(PublicKey key, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.Any(d => d.PublicKey == key));

    public Task<IReadOnlyList<Device>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Device>>(_store.Values.OrderByDescending(d => d.RegisteredAt).ToList());

    public Task SaveAsync(Device device, CancellationToken ct = default) =>
        Task.CompletedTask; // EF change tracking not needed; mutations are in-place

    public Task UpdateAsync(Device device, CancellationToken ct = default) =>
        Task.CompletedTask; // In-memory: mutations are in-place via the stored reference
}
