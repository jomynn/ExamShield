using MediatR;

namespace ExamShield.Application.Queries.PublicVerifyCapture;

public sealed record PublicVerifyCaptureQuery(
    Guid? CaptureId = null,
    string? HashHex = null)
    : IRequest<PublicVerifyResult>;

public sealed record PublicVerifyResult(
    Guid CaptureId,
    bool IsValid,
    bool HashValid,
    bool SignatureValid,
    bool IsUploaded,
    DateTimeOffset? CapturedAt
);
