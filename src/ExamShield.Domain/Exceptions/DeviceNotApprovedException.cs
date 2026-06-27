namespace ExamShield.Domain.Exceptions;

public sealed class DeviceNotApprovedException(Guid deviceId)
    : Exception($"Device {deviceId} has not been approved and cannot register captures.");
