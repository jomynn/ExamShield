using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IDeviceRepository
{
    Task AddAsync(Device device, CancellationToken ct = default);
    Task<Device?> GetByIdAsync(DeviceId id, CancellationToken ct = default);
    Task<IReadOnlyList<Device>> ListAllAsync(CancellationToken ct = default);
    Task SaveAsync(Device device, CancellationToken ct = default);
    Task UpdateAsync(Device device, CancellationToken ct = default);
}
