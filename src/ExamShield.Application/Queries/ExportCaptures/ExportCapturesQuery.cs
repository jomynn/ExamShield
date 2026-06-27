using ExamShield.Domain.Entities;
using MediatR;

namespace ExamShield.Application.Queries.ExportCaptures;

public sealed record ExportCapturesResult(string Csv, string Filename);

public sealed record ExportCapturesQuery(
    Guid? ExamId = null,
    CaptureStatus? Status = null) : IRequest<ExportCapturesResult>;
