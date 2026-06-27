namespace ExamShield.Domain.Exceptions;

public sealed class DuplicateDevicePublicKeyException()
    : Exception("A device with this public key is already registered.");
