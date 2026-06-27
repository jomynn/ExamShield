using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

// CaptureStatus is a top-level enum declared in Capture.cs (no sub-namespace)
using CaptureStatus = ExamShield.Domain.Entities.CaptureStatus;

namespace ExamShield.Application.Queries.GetExamReport;

public sealed class GetExamReportQueryHandler(
    IExamRepository exams,
    ICaptureRepository captures,
    IOcrResultRepository ocrResults,
    IScoreRepository scores,
    IReviewRequestRepository reviewRequests)
    : IRequestHandler<GetExamReportQuery, GetExamReportResult>
{
    public async Task<GetExamReportResult> Handle(GetExamReportQuery query, CancellationToken ct)
    {
        var examId = new ExamId(query.ExamId);
        var exam = await exams.GetByIdAsync(examId, ct)
            ?? throw new KeyNotFoundException($"Exam '{query.ExamId}' not found.");

        var captureList = await captures.ListByExamIdAsync(examId, ct);
        var captureIds = captureList.Select(c => c.Id).ToList();

        var ocrList = captureIds.Count > 0
            ? await ocrResults.ListByCaptureIdsAsync(captureIds, ct)
            : (IReadOnlyList<OcrResult>)[];

        var scoreList = await scores.GetByExamIdAsync(examId, ct);

        var reviewList = captureIds.Count > 0
            ? await reviewRequests.ListByCaptureIdsAsync(captureIds, ct)
            : (IReadOnlyList<ReviewRequest>)[];

        var avgConfidence = ocrList.Count > 0
            ? ocrList.Average(o => o.OverallConfidence.Value)
            : 0.0;

        var avgScore = scoreList.Count > 0
            ? scoreList.Average(s => s.Percentage)
            : 0.0;

        return new GetExamReportResult(
            ExamId: exam.Id.Value,
            ExamName: exam.Name,
            ExamStatus: exam.Status.ToString(),
            TotalQuestions: exam.TotalQuestions,
            GeneratedAt: DateTimeOffset.UtcNow,
            TotalCaptures: captureList.Count,
            UploadedCaptures: captureList.Count(c => c.Status >= CaptureStatus.Uploaded),
            VerifiedCaptures: captureList.Count(c => c.Status == CaptureStatus.Verified),
            TamperedCaptures: captureList.Count(c => c.Status == CaptureStatus.Tampered),
            TotalOcrProcessed: ocrList.Count,
            OcrAverageConfidence: avgConfidence,
            LowConfidenceCount: ocrList.Count(o => o.RequiresManualReview),
            TotalScored: scoreList.Count,
            AverageScorePercentage: avgScore,
            HighestScorePercentage: scoreList.Count > 0 ? scoreList.Max(s => s.Percentage) : 0.0,
            LowestScorePercentage: scoreList.Count > 0 ? scoreList.Min(s => s.Percentage) : 0.0,
            TotalReviewRequests: reviewList.Count);
    }
}
