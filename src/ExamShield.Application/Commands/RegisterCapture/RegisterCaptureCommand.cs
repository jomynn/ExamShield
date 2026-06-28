using MediatR;

namespace ExamShield.Application.Commands.RegisterCapture;

public sealed record RegisterCaptureCommand(
    Guid ExamId,
    Guid StudentId,
    Guid DeviceId,
    int PageNumber,
    string HashHex,
    byte[] SignatureBytes,
    Guid? InvigilatorId = null
) : IRequest<RegisterCaptureResult>;

public sealed record RegisterCaptureResult(Guid CaptureId);
