using MediatR;

namespace ExamShield.Application.Queries.GetStudentCertificate;

public sealed record GetStudentCertificateQuery(Guid CaptureId) : IRequest<GetStudentCertificateResult>;

public sealed record GetStudentCertificateResult(byte[] PdfBytes, string Filename);
