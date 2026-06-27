using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly ExamShieldDbContext _context;

    public DeviceRepository(ExamShieldDbContext context) => _context = context;

    public async Task AddAsync(Device device, CancellationToken ct = default)
    {
        await _context.Devices.AddAsync(device, ct);
        await _context.SaveChangesAsync(ct);
    }

    public Task<Device?> GetByIdAsync(DeviceId id, CancellationToken ct = default) =>
        _context.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Device>> ListAllAsync(CancellationToken ct = default) =>
        await _context.Devices.OrderByDescending(d => d.RegisteredAt).ToListAsync(ct);

    public async Task SaveAsync(Device device, CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct);

    public async Task UpdateAsync(Device device, CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct);
}
