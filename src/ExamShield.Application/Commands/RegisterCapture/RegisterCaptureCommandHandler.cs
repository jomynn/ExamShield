using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.RegisterCapture;

public sealed class RegisterCaptureCommandHandler
    : IRequestHandler<RegisterCaptureCommand, RegisterCaptureResult>
{
    private readonly ICaptureRepository _repository;
    private readonly IDeviceRepository _devices;
    private readonly ISignatureVerificationService _sigService;
    private readonly IAuditLogRepository _auditLog;
    private readonly IExamRepository _exams;
    private readonly IExamCandidateRepository _candidates;

    public RegisterCaptureCommandHandler(
        ICaptureRepository repository,
        IDeviceRepository devices,
        ISignatureVerificationService sigService,
        IAuditLogRepository auditLog,
        IExamRepository exams,
        IExamCandidateRepository candidates)
    {
        _repository = repository;
        _devices = devices;
        _sigService = sigService;
        _auditLog = auditLog;
        _exams = exams;
        _candidates = candidates;
    }

    public async Task<RegisterCaptureResult> Handle(
        RegisterCaptureCommand command, CancellationToken ct)
    {
        var hash = Hash.FromHex(command.HashHex);
        var signature = new Signature(command.SignatureBytes);
        var examId = new ExamId(command.ExamId);
        var studentId = new StudentId(command.StudentId);
        var deviceId = new DeviceId(command.DeviceId);
        var pageNumber = new PageNumber(command.PageNumber);

        var exam = await _exams.GetByIdAsync(examId, ct)
            ?? throw new KeyNotFoundException($"Exam '{command.ExamId}' not found.");

        if (exam.Status != ExamStatus.Active)
            throw new ExamNotActiveException(command.ExamId);

        if (exam.EndsAt is not null && DateTimeOffset.UtcNow > exam.EndsAt)
            throw new ExamExpiredException(command.ExamId, exam.EndsAt.Value);

        if (!await _candidates.ExistsAsync(examId, studentId, ct))
            throw new StudentNotEnrolledException(command.ExamId, command.StudentId);

        var device = await _devices.GetByIdAsync(deviceId, ct)
            ?? throw new DeviceNotFoundException(command.DeviceId);

        if (device.Status != DeviceStatus.Approved)
            throw new DeviceNotApprovedException(command.DeviceId);

        if (!_sigService.Verify(hash, signature, device.PublicKey))
            throw new InvalidSignatureException(command.DeviceId);

        if (await _repository.ExistsByStudentExamPageAsync(studentId, examId, pageNumber, ct))
            throw new DuplicateCaptureException(command.ExamId, command.StudentId, command.PageNumber);

        var invigilatorId = command.InvigilatorId.HasValue
            ? new UserId(command.InvigilatorId.Value)
            : (UserId?)null;
        var capture = Capture.Create(examId, studentId, deviceId, pageNumber, hash, signature, invigilatorId);

        await _repository.AddAsync(capture, ct);
        await _auditLog.AppendAsync(
            AuditLog.Record(AuditAction.CaptureRegistered, captureId: capture.Id), ct);

        return new RegisterCaptureResult(capture.Id.Value);
    }
}
