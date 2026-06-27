using MediatR;

namespace ExamShield.Application.Queries.GetExamReport;

public sealed record GetExamReportResult(
    Guid ExamId,
    string ExamName,
    string ExamStatus,
    int TotalQuestions,
    DateTimeOffset GeneratedAt,
    int TotalCaptures,
    int UploadedCaptures,
    int VerifiedCaptures,
    int TamperedCaptures,
    int TotalOcrProcessed,
    double OcrAverageConfidence,
    int LowConfidenceCount,
    int TotalScored,
    double AverageScorePercentage,
    double HighestScorePercentage,
    double LowestScorePercentage,
    int TotalReviewRequests);

public sealed record GetExamReportQuery(Guid ExamId) : IRequest<GetExamReportResult>;
