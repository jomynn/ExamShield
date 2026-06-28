using ExamShield.Domain.Events;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Entities;

public enum CaptureStatus { Created, Uploaded, Verified, Tampered }

public sealed class Capture : AggregateRoot
{
    public CaptureId Id { get; private set; } = null!;
    public ExamId ExamId { get; private set; } = null!;
    public StudentId StudentId { get; private set; } = null!;
    public DeviceId DeviceId { get; private set; } = null!;
    public UserId? InvigilatorId { get; private set; }
    public PageNumber PageNumber { get; private set; } = null!;
    public Hash ExpectedHash { get; private set; } = null!;
    public Signature Signature { get; private set; } = null!;
    public CaptureStatus Status { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }
    public string? StorageKey { get; private set; }

    private Capture() { } // required by EF Core

    private Capture(
        CaptureId id, ExamId examId, StudentId studentId, DeviceId deviceId,
        PageNumber pageNumber, Hash hash, Signature signature, DateTimeOffset capturedAt,
        UserId? invigilatorId)
    {
        Id = id; ExamId = examId; StudentId = studentId; DeviceId = deviceId;
        PageNumber = pageNumber; ExpectedHash = hash; Signature = signature;
        CapturedAt = capturedAt; Status = CaptureStatus.Created;
        InvigilatorId = invigilatorId;
    }

    public static Capture Create(
        ExamId examId, StudentId studentId, DeviceId deviceId,
        PageNumber pageNumber, Hash hash, Signature signature,
        UserId? invigilatorId = null)
    {
        ArgumentNullException.ThrowIfNull(examId, nameof(examId));
        ArgumentNullException.ThrowIfNull(studentId, nameof(studentId));
        ArgumentNullException.ThrowIfNull(deviceId, nameof(deviceId));
        ArgumentNullException.ThrowIfNull(pageNumber, nameof(pageNumber));
        ArgumentNullException.ThrowIfNull(hash, nameof(hash));
        ArgumentNullException.ThrowIfNull(signature, nameof(signature));

        var capture = new Capture(
            CaptureId.New(), examId, studentId, deviceId,
            pageNumber, hash, signature, DateTimeOffset.UtcNow, invigilatorId);

        capture.AddDomainEvent(new CaptureCreated(capture.Id, capture.ExamId, capture.StudentId));
        return capture;
    }

    public void RecordUpload(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));

        if (Status != CaptureStatus.Created)
            throw new DuplicateUploadException(Id.Value);

        StorageKey = storageKey;
        Status = CaptureStatus.Uploaded;
        AddDomainEvent(new ImageUploaded(Id, storageKey));
    }

    public bool VerifyIntegrity(Hash currentHash)
    {
        ArgumentNullException.ThrowIfNull(currentHash, nameof(currentHash));

        if (ExpectedHash == currentHash)
        {
            Status = CaptureStatus.Verified;
            return true;
        }

        Status = CaptureStatus.Tampered;
        AddDomainEvent(new TamperingDetected(Id, ExpectedHash, currentHash));
        return false;
    }

    public void FlagAsTampered(string reason)
    {
        if (Status == CaptureStatus.Tampered)
            throw new CaptureAlreadyTamperedException(Id.Value);

        Status = CaptureStatus.Tampered;
        AddDomainEvent(new TamperingDetected(Id, ExpectedHash, ExpectedHash));
    }
}
