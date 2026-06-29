using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetStudentCertificate;

public sealed class GetStudentCertificateQueryHandler(
    IScoreRepository scores,
    IExamRepository exams,
    IStudentCertificateService certificateService)
    : IRequestHandler<GetStudentCertificateQuery, GetStudentCertificateResult>
{
    public async Task<GetStudentCertificateResult> Handle(
        GetStudentCertificateQuery query, CancellationToken ct)
    {
        var captureId = new CaptureId(query.CaptureId);

        var score = await scores.GetByCaptureIdAsync(captureId, ct)
            ?? throw new KeyNotFoundException($"No published score found for capture {query.CaptureId}.");

        if (!score.IsPublished)
            throw new InvalidOperationException("Results have not been published yet.");

        var exam = await exams.GetByIdAsync(score.ExamId, ct)
            ?? throw new KeyNotFoundException($"Exam {score.ExamId.Value} not found.");

        var data = new CertificateData(
            ExamName: exam.Name,
            StudentId: score.StudentId.Value.ToString(),
            CaptureId: score.CaptureId.Value.ToString(),
            CorrectAnswers: score.CorrectAnswers,
            TotalQuestions: score.TotalQuestions,
            Percentage: score.Percentage,
            ScoredAt: score.ScoredAt);

        var pdf = certificateService.Generate(data);
        var filename = $"certificate-{score.CaptureId.Value:N}.pdf";

        return new GetStudentCertificateResult(pdf, filename);
    }
}
