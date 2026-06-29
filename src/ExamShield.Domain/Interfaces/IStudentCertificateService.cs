namespace ExamShield.Domain.Interfaces;

public sealed record CertificateData(
    string ExamName,
    string StudentId,
    string CaptureId,
    int CorrectAnswers,
    int TotalQuestions,
    double Percentage,
    DateTimeOffset ScoredAt);

public interface IStudentCertificateService
{
    byte[] Generate(CertificateData data);
}
