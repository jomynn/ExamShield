using ExamShield.Domain.Enums;
using ExamShield.Domain.Events;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Entities;

public sealed class Device : AggregateRoot
{
    public DeviceId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public PublicKey PublicKey { get; private set; } = null!;
    public DateTimeOffset RegisteredAt { get; private set; }
    public DeviceStatus Status { get; private set; }
    public bool IsActive => Status == DeviceStatus.Approved;
    public DateTimeOffset? LastSeenAt { get; private set; }

    private Device() { } // EF Core

    public static Device Register(string name, PublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(publicKey, nameof(publicKey));

        var device = new Device
        {
            Id = DeviceId.New(),
            Name = name,
            PublicKey = publicKey,
            RegisteredAt = DateTimeOffset.UtcNow,
            Status = DeviceStatus.Pending
        };

        device.AddDomainEvent(new DeviceRegistered(device.Id));
        return device;
    }

    public void Approve()  => Status = DeviceStatus.Approved;
    public void Disable()  => Status = DeviceStatus.Disabled;
    public void Enable()   => Status = DeviceStatus.Approved;

    public void RecordHeartbeat()
    {
        if (!IsActive)
            throw new InvalidOperationException($"Device {Id.Value} is disabled and cannot send heartbeats.");
        LastSeenAt = DateTimeOffset.UtcNow;
    }
}
